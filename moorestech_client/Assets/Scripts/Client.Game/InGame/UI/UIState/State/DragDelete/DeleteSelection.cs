using Mooresmaster.Localization.Generated;
using Client.Game.InGame.UI.UIState.State.DragDelete.Confirm;
using System;
using UniRx;
using Client.Game.InGame.UI.UIState.State.DragDelete.Completion;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using UnityEngine;
namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    public class DeleteSelection
    {
        private readonly BuildOperationHistory _buildOperationHistory;
        private readonly IRemovalRestoreSender _restoreSender;
        // 論理削除キーで重複排除する（同一機械の複数メッシュ子などを1件に集約）
        // Dedupe by logical delete key so multiple mesh children of one machine collapse into one
        private readonly Dictionary<object, IDeleteTarget> _selectedTargets = new();
        private bool _strokeCanceled;
        private readonly DeleteResultTracker _results = new();
        private readonly Subject<DeleteCompletion> _deleteCompleted = new();
        public IObservable<DeleteCompletion> OnDeleteCompleted => _deleteCompleted;
        private readonly Subject<int> _selectionChanged = new();
        public IObservable<int> OnSelectionChanged => _selectionChanged;
        public DeleteAimFilter AimFilter { get; private set; } = DeleteAimFilter.Frontmost;
        public int Revision { get; private set; }
        public int Count => _selectedTargets.Count;
        // 今赤くしているホバー対象（選択済みとは別管理）
        // The hovered target currently shown red (tracked apart from the selection)
        public IDeleteTarget HoverPreview { get; private set; }
        public DeleteSelection(BuildOperationHistory buildOperationHistory, IRemovalRestoreSender restoreSender)
        {
            _buildOperationHistory = buildOperationHistory;
            _restoreSender = restoreSender;
            _deleteCompleted.Subscribe(_results.Receive);
        }
        // ストローク開始: キャンセル解除とカテゴリー固定のリセットだけで、選択は消さない
        // Stroke start: only revives the stroke and resets the category fix; the selection survives
        public void BeginStroke()
        {
            _strokeCanceled = false;
            AimFilter = DeleteAimFilter.Frontmost;
        }
        // 左ボタン解放: カテゴリー固定を最前面へ戻す（離した直後のホバーが別カテゴリーも拾えるように）
        // Button release: return the category fix to frontmost so the next hover can pick any category
        public void EndStroke()
        {
            AimFilter = DeleteAimFilter.Frontmost;
        }
        public DeleteAddOutcome TryAddTarget(IDeleteTarget target)
        {
            if (_strokeCanceled) return DeleteAddOutcome.StrokeDead;
            if (_results.IsPending(target.GetDeleteTargetKey())) return DeleteAddOutcome.NotRemovable(LocalizationKeys.Ui.Tooltip.DeleteRequestPending);
            if (!target.IsRemovable(out var denyReason)) return DeleteAddOutcome.NotRemovable(denyReason);
            // ストロークカテゴリーと異なるカテゴリーは追加しない（混在防止）
            // Reject a target whose category differs from the stroke category (prevents mixing)
            if (!AimFilter.Accepts(target)) return DeleteAddOutcome.CategoryMismatch();
            // 最初の追加でカテゴリー固定
            // Lock the category on the first addition
            if (!AimFilter.IsCategoryRequired) AimFilter = DeleteAimFilter.Category(target.GetDestructionCategory());
            return Add(target) ? DeleteAddOutcome.Added : DeleteAddOutcome.AlreadySelected;
        }
        // 箱の対象（撤去可否は Resolver が判定済み）をカテゴリーを問わず足す。足した件数を返す
        // Add box targets (already filtered for removability by the resolver) regardless of category; returns how many were added
        public int AddBoxTargets(IReadOnlyList<IDeleteTarget> targets)
        {
            var added = 0;
            foreach (var target in targets)
            {
                if (Add(target)) added++;
            }
            return added;
        }
        // ホバー替え: 旧ホバーが選択済みでなければ赤を戻し、新ホバーを赤にする（null で解除）
        // Hover swap: un-red the previous hover unless it is selected, then red the new one (null clears)
        public void SetHoverPreview(IDeleteTarget hovered)
        {
            if (ReferenceEquals(HoverPreview, hovered)) return;
            ResetHoverPreview();
            HoverPreview = hovered;
            if (HoverPreview != null && !Contains(HoverPreview)) HoverPreview.SetRemovePreviewing();
        }
        private bool Contains(IDeleteTarget target)
        {
            return _selectedTargets.ContainsKey(target.GetDeleteTargetKey());
        }
        // 全選択を解除しストロークを無効化（ESC）
        // Clear all selections and cancel the stroke (ESC)
        public void CancelSelection()
        {
            foreach (var target in _selectedTargets.Values)
            {
                if (IsDestroyed(target, "cancel")) continue;
                target.ResetMaterial();
            }
            _selectedTargets.Clear();
            ResetHoverPreview();
            HoverPreview = null;
            _strokeCanceled = true;
            AimFilter = DeleteAimFilter.Frontmost;
            Revision++;
            _selectionChanged.OnNext(Revision);
        }
        public void CommitDelete()
        {
            var committed = new List<IDeleteTarget>();
            foreach (var target in _selectedTargets.Values)
            {
                if (!IsDestroyed(target, "commit")) committed.Add(target);
            }
            // 採取と購読を済ませてからプレビューを戻し、送信する
            // Capture and subscribe before clearing previews and sending
            var batch = new DeleteCommitBatch(committed, _buildOperationHistory, _restoreSender);
            batch.OnCompleted.Subscribe(_deleteCompleted.OnNext);
            CancelSelection();
            foreach (var target in committed) _deleteCompleted.OnNext(new DeleteCompletion(target.GetDeleteTargetKey(), DeleteCompletionKind.Pending, null));
            batch.Send();
        }
        internal LocalizationKey? UpdateStroke(DeleteAimResult aim)
        {
            if (_strokeCanceled) return null; // silent-ok: 取消済みストローク / ESCで選択表示を解除済み
            return aim.Target == null ? aim.GetDragDenyReason() : TryAddTarget(aim.Target).DenyReason;
        }
        internal DeleteHoldSubject UpdateHoverPreview(IDeleteTarget target, out LocalizationKey? reason)
        {
            SetHoverPreview(target);
            reason = null;
            var removable = target != null && target.IsRemovable(out reason);
            if (HasSelection()) return DeleteHoldSubject.None; // silent-ok: 集合の長押し対象は変更通知で更新済み / serviceが購読する
            return removable ? DeleteHoldSubject.Hovered(target.GetDeleteTargetKey()) : DeleteHoldSubject.None;
        }
        internal bool TryCommit(DeleteHoldSubject subject, out LocalizationKey? reason)
        {
            reason = null;
            if (subject.Kind == DeleteHoldSubjectKind.Hovered)
            {
                BeginStroke();
                var outcome = TryAddTarget(HoverPreview);
                if (!outcome.IsAdded)
                {
                    reason = outcome.DenyReason;
                    Debug.Log($"[DragDelete] implicit hover commit refused: {outcome.Kind}");
                    SetHoverPreview(null);
                    return false;
                }
            }
            CommitDelete();
            return true;
        }
        internal bool TryCancelSelection()
        {
            if (!HasSelection()) return false; // silent-ok: 解除対象なし / 呼出元がモード離脱を判断
            CancelSelection();
            return true;
        }
        public bool HasSelection() => 0 < _selectedTargets.Count;
        // 既に同じ論理対象が選択済みなら何もしない（Revisionも進めない）
        // No-op when the same logical target is already selected (the revision stays)
        private bool Add(IDeleteTarget target)
        {
            var key = target.GetDeleteTargetKey();
            if (_selectedTargets.ContainsKey(key) || _results.IsPending(key)) return false; // silent-ok: 選択済み/結果待ちの対象を重複送信しない / 選択赤と結果ツールチップで表示
            // 破棄済みホバーはキーを読まず参照を解除する
            // Clear a destroyed hover without reading its key
            if (HoverPreview != null && IsDestroyed(HoverPreview, "hover")) HoverPreview = null;
            // 同じ論理キーでも旧ホバー自身の要求を解放して移譲する
            // Release the actual hover requester before transferring the same logical key
            if (HoverPreview != null && Equals(HoverPreview.GetDeleteTargetKey(), key))
            {
                ResetHoverPreview();
                HoverPreview = null;
            }
            _selectedTargets.Add(key, target);
            target.SetRemovePreviewing();
            Revision++;
            _selectionChanged.OnNext(Revision);
            return true;
        }
        // ホバーの赤を戻す（選択済みなら選択側が赤を持つので触らない・破棄済みは飛ばす）
        // Un-red the hover (a selected one keeps its red via the selection; skip destroyed ones)
        private void ResetHoverPreview()
        {
            if (HoverPreview == null) return;
            if (IsDestroyed(HoverPreview, "hover")) return;
            if (!Contains(HoverPreview)) HoverPreview.ResetMaterial();
        }
        // 破棄済みUnity対象を警告付きで判定
        // Detect destroyed Unity targets with a warning
        private static bool IsDestroyed(IDeleteTarget target, string phase)
        {
            if (target is not UnityEngine.Object unityTarget || unityTarget != null) return false;
            Debug.LogWarning($"[DragDelete] target was destroyed before {phase}");
            return true;
        }
    }
}
