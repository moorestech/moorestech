using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    /// <summary>
    ///     削除ツールの選択集合。なぞり・箱で積み上げ、ストロークを跨いで持続し、確定時のUndo記録も担う。赤プレビューの付け外しはここだけが行う
    ///     The delete tool's selection set: accumulated by strokes and boxes, persists across strokes, records undo on commit; only this class toggles red previews
    /// </summary>
    public class DeleteSelection
    {
        private readonly BuildOperationHistory _buildOperationHistory;
        private readonly IRemovalRestoreSender _restoreSender;

        // 論理削除キーで重複排除する（同一機械の複数メッシュ子などを1件に集約）
        // Dedupe by logical delete key so multiple mesh children of one machine collapse into one
        private readonly Dictionary<object, IDeleteTarget> _selectedTargets = new();
        private bool _strokeCanceled;

        // ストロークごとに最初の対象のカテゴリーで照準を固定
        // Fix aim to the first target's category, per stroke
        public DeleteAimFilter AimFilter { get; private set; } = DeleteAimFilter.Frontmost;

        // 集合が変わるたびに進む。長押しの「対象が変わった」検知に使う
        // Advances whenever the set changes; the hold confirm uses it to detect a changed subject
        public int Revision { get; private set; }
        public int Count => _selectedTargets.Count;

        // 今赤くしているホバー対象（選択済みとは別管理）
        // The hovered target currently shown red (tracked apart from the selection)
        public IDeleteTarget HoverPreview { get; private set; }

        public DeleteSelection(BuildOperationHistory buildOperationHistory, IRemovalRestoreSender restoreSender)
        {
            _buildOperationHistory = buildOperationHistory;
            _restoreSender = restoreSender;
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
            if (!target.IsRemovable(out var denyReason)) return DeleteAddOutcome.NotRemovable(denyReason);

            // ストロークカテゴリーと異なるカテゴリーは追加しない（混在防止）
            // Reject a target whose category differs from the stroke category (prevents mixing)
            if (!AimFilter.Accepts(target)) return DeleteAddOutcome.CategoryMismatch();

            // 最初の追加でストロークのカテゴリーを固定する
            // Fix the stroke category on the first added target
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

        // 選択を全てリセットしてストロークを死に状態にする（ESC操作）
        // Reset all selections and mark the stroke as canceled (ESC behavior)
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
        }

        // 選択を一括削除し、Ctrl+Z用のUndo履歴も記録する
        // Delete the whole selection and record the undo history for Ctrl+Z
        public void CommitDelete()
        {
            // 削除送信後に端点が消え得るため撤去物を先に記録する
            // Record removed objects before sends can remove their endpoints
            var committed = new List<IDeleteTarget>();
            foreach (var target in _selectedTargets.Values)
            {
                if (IsDestroyed(target, "commit")) continue;
                committed.Add(target);
            }
            var record = RemoveOperationRecord.CreateFrom(committed, _restoreSender);
            foreach (var target in committed)
            {
                // Delete はサーバー往復の非同期なので即座に赤プレビューだけ戻す
                // Delete is async over the server, so we just clear the red preview immediately
                target.Delete();
                target.ResetMaterial();
            }
            _selectedTargets.Clear();
            ResetHoverPreview();
            HoverPreview = null;
            AimFilter = DeleteAimFilter.Frontmost;
            Revision++;

            // Ctrl+Z用のUndo履歴を記録（空バッチはPushしない）
            // Record the undo history for Ctrl+Z (skip empty batches)
            if (record.HasRemovedObjects) _buildOperationHistory.Push(record);
        }

        public bool IsStrokeAlive()
        {
            return !_strokeCanceled;
        }

        public bool HasSelection()
        {
            return 0 < _selectedTargets.Count;
        }

        // 既に同じ論理対象が選択済みなら何もしない（Revisionも進めない）
        // No-op when the same logical target is already selected (the revision stays)
        private bool Add(IDeleteTarget target)
        {
            var key = target.GetDeleteTargetKey();
            if (_selectedTargets.ContainsKey(key)) return false;
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

        // Unity オブジェクトの破棄済み判定（墓標は警告を出して読み飛ばす）
        // Unity destroyed-object check (tombstones are skipped with a warning)
        private static bool IsDestroyed(IDeleteTarget target, string phase)
        {
            if (target is not Object unityTarget || unityTarget != null) return false;
            Debug.LogWarning($"[DragDelete] target was destroyed before {phase}");
            return true;
        }
    }
}
