using Mooresmaster.Localization.Generated;
using Client.Game.InGame.Block;
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
        private readonly DeletePreviewResetRecovery _resetRecovery;
        private readonly SingleHoverDeletePreview _hoverPreview;
        private readonly DeletePreviewReleaseQueue _pendingResets;
        // 論理削除キーで重複排除する（同一機械の複数メッシュ子などを1件に集約）
        // Dedupe by logical delete key so multiple mesh children of one machine collapse into one
        private readonly Dictionary<object, IDeleteTarget> _selectedTargets = new();
        private readonly CompositeDisposable _placeAnimationSubscriptions = new();
        private bool _strokeCanceled;
        private IDeleteTarget _hoverTarget;
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
        public IDeleteTarget HoverPreview => _hoverPreview.Current;
        public DeleteSelection(BuildOperationHistory buildOperationHistory, IRemovalRestoreSender restoreSender, DeletePreviewResetRecovery resetRecovery)
        {
            _buildOperationHistory = buildOperationHistory;
            _restoreSender = restoreSender;
            _resetRecovery = resetRecovery;
            _hoverPreview = new SingleHoverDeletePreview(resetRecovery);
            _pendingResets = new DeletePreviewReleaseQueue(resetRecovery);
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
        public void EndStroke() => AimFilter = DeleteAimFilter.Frontmost;
        public DeleteAddOutcome TryAddTarget(IDeleteTarget target)
        {
            if (_strokeCanceled) return DeleteAddOutcome.StrokeDead;
            if (!IsTargetSelectable(target, out var denyReason)) return DeleteAddOutcome.NotRemovable(denyReason);
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
            _hoverTarget = hovered;
            if (hovered == null || _selectedTargets.ContainsKey(hovered.GetDeleteTargetKey())) _hoverPreview.Release();
            else _hoverPreview.Hover(hovered);
        }
        public void CancelSelection()
        {
            ClearSelectionState();
            ReleasePreviews("[DragDelete] selected target was destroyed before cancel");
        }
        private void ClearSelectionState()
        {
            // 戻し処理より先に論理状態と長押しsubjectを解除する
            // Clear logical state and the hold subject before any reset can throw
            _pendingResets.Enqueue(_selectedTargets.Values);
            _selectedTargets.Clear();
            _placeAnimationSubscriptions.Clear();
            _hoverTarget = null;
            _strokeCanceled = true;
            AimFilter = DeleteAimFilter.Frontmost;
            Revision++;
            _selectionChanged.OnNext(Revision);
        }
        private void ReleasePreviews(string destroyedWarning)
        {
            try { _pendingResets.Drain(destroyedWarning); }
            finally { _hoverPreview.Release(); }
        }
        public void CommitDelete()
        {
            var committed = new List<IDeleteTarget>();
            foreach (var target in _selectedTargets.Values)
            {
                if (DeleteTargetLiveness.IsAliveOrWarn(target, "[DragDelete] selected target was destroyed before commit")) committed.Add(target);
            }
            // 採取後に論理選択を空にし、表示解除に失敗しても要求とUndoを失わない
            // Clear logical selection after capture so failed preview resets cannot lose requests or undo
            var batch = new DeleteCommitBatch(committed, _buildOperationHistory, _restoreSender);
            batch.OnCompleted.Subscribe(_deleteCompleted.OnNext);
            ClearSelectionState();
            foreach (var target in committed) _deleteCompleted.OnNext(new DeleteCompletion(target.GetDeleteTargetKey(), DeleteCompletionKind.Pending, null));
            batch.Send();
            ReleasePreviews("[DragDelete] selected target was destroyed before reset");
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
            var removable = target != null && IsTargetSelectable(target, out reason);
            if (HasSelection()) return DeleteHoldSubject.None; // silent-ok: 集合の長押し対象は変更通知で更新済み / serviceが購読する
            return removable ? DeleteHoldSubject.Hovered(target.GetDeleteTargetKey()) : DeleteHoldSubject.None;
        }
        internal bool TryCommit(DeleteHoldSubject subject, out LocalizationKey? reason)
        {
            reason = null;
            if (subject.Kind == DeleteHoldSubjectKind.Hovered)
            {
                BeginStroke();
                var outcome = TryAddTarget(_hoverTarget);
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
        // 保留と対象自身の可否を箱・ホバー・追加で共有する
        // Share pending and target eligibility across box, hover and addition
        internal bool IsTargetSelectable(IDeleteTarget target, out LocalizationKey? reason)
        {
            reason = LocalizationKeys.Ui.Tooltip.DeleteRequestPending;
            return !_results.IsPending(target.GetDeleteTargetKey()) && target.IsRemovable(out reason);
        }
        public bool HasSelection() => 0 < _selectedTargets.Count;
        // 既に同じ論理対象が選択済みなら何もしない（Revisionも進めない）
        // No-op when the same logical target is already selected (the revision stays)
        private bool Add(IDeleteTarget target)
        {
            var key = target.GetDeleteTargetKey();
            if (_selectedTargets.ContainsKey(key) || !IsTargetSelectable(target, out _)) return false; // silent-ok: 選択済み/結果待ちの対象を重複送信しない / 選択赤と結果ツールチップで表示
            // 同一論理対象への移譲前に旧ホバーを必ず手放す
            // Release the old hover before transferring ownership of the same logical target
            if (HoverPreview != null && ((HoverPreview is UnityEngine.Object view && view == null) || Equals(HoverPreview.GetDeleteTargetKey(), key))) _hoverPreview.Release();
            _selectedTargets.Add(key, target);
            if (_resetRecovery.CanPreview(target)) target.SetRemovePreviewing();
            // 設置演出で戻された赤を、選択が続く間だけ復元する
            // Restore red after placement animation only while selection still owns the target
            if (target is BlockGameObjectChild child)
            {
                child.BlockGameObject.OnFinishedPlaceAnimation.Take(1).Subscribe(_ =>
                {
                    if (_selectedTargets.TryGetValue(key, out var selected) && ReferenceEquals(selected, target) &&
                        DeleteTargetLiveness.IsAliveOrWarn(target, "[DragDelete] selected target destroyed after placement") && _resetRecovery.CanPreview(target)) target.SetRemovePreviewing();
                }).AddTo(_placeAnimationSubscriptions);
            }
            Revision++;
            _selectionChanged.OnNext(Revision);
            return true;
        }
    }
}
