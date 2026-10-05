using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    /// <summary>
    ///     ドラッグ中に選択された削除対象を管理するモデルクラス。削除確定時のUndo履歴記録も担う
    ///     Model class that manages delete targets selected during a drag; also records undo history on commit
    /// </summary>
    public class DragDeleteSelection
    {
        private readonly BuildOperationHistory _buildOperationHistory;
        private readonly IRemovalRestoreSender _restoreSender;

        // 論理削除キーで重複排除する（同一機械の複数メッシュ子などを1件に集約）
        // Dedupe by logical delete key so multiple mesh children of one machine collapse into one
        private readonly Dictionary<object, IDeleteTarget> _selectedTargets = new();
        private bool _canceled;

        // 最初の対象のカテゴリーで照準を固定
        // Fix aim to the first target's category
        public DeleteAimFilter AimFilter { get; private set; } = DeleteAimFilter.Frontmost;

        public DragDeleteSelection(BuildOperationHistory buildOperationHistory, IRemovalRestoreSender restoreSender)
        {
            _buildOperationHistory = buildOperationHistory;
            _restoreSender = restoreSender;
        }

        // 新しいドラッグ開始時に選択・キャンセル状態・セッションカテゴリーをリセットする
        // Reset selection, canceled state, and session category when a new drag begins
        public void BeginDrag()
        {
            _selectedTargets.Clear();
            _canceled = false;
            AimFilter = DeleteAimFilter.Frontmost;
        }

        // 対象を選択へ追加する。削除可否・カテゴリー整合をまとめて判定し、追加不可なら拒否理由を返す（理由なし拒否はnull）
        // Add a target to the selection; judges removability and category together, returning a deny reason when rejected (null when there is none)
        public bool TryAddTarget(IDeleteTarget target, out LocalizationKey? denyReason)
        {
            denyReason = null;
            if (_canceled) return false;

            // 削除不可なら対象由来の理由をそのまま返す
            // Rejected as non-removable; surface the target's own reason
            if (!target.IsRemovable(out denyReason)) return false;

            // セッションカテゴリーと異なるカテゴリーは追加しない（混在防止）
            // Reject a target whose category differs from the session category (prevents mixing)
            if (!IsCategoryCompatible(target))
            {
                denyReason = LocalizationKeys.Ui.Delete.DifferentCategorySelection;
                return false;
            }

            // 既に同じ論理対象が選択済みなら重複追加しない（拒否理由なしの成功扱い）
            // Skip when already selected: success without a deny reason (prevents duplicate Delete)
            var key = target.GetDeleteTargetKey();
            if (_selectedTargets.ContainsKey(key)) return true;

            // 最初の追加でセッションカテゴリーを固定する
            // Fix the session category on the first added target
            if (!AimFilter.IsCategoryRequired) AimFilter = DeleteAimFilter.Category(target.GetDestructionCategory());

            _selectedTargets.Add(key, target);
            target.SetRemovePreviewing();
            return true;
        }

        // このセッションに追加可能なカテゴリーか（未選択時は何でも可、以降は同一カテゴリーのみ）
        // Whether the target's category can join this session (anything while empty, then same category only)
        private bool IsCategoryCompatible(IDeleteTarget target)
        {
            return AimFilter.Accepts(target);
        }

        // 選択を全てリセットしてキャンセル状態にする（ESC操作）
        // Reset all selections and mark as canceled (ESC behavior)
        public void CancelSelection()
        {
            foreach (var target in _selectedTargets.Values)
            {
                if (target is Object unityTarget && unityTarget == null)
                {
                    Debug.LogWarning("[DragDelete] selected target was destroyed before cancel");
                    continue;
                }
                target.ResetMaterial();
            }

            _selectedTargets.Clear();
            _canceled = true;
            AimFilter = DeleteAimFilter.Frontmost;
        }

        // 選択を一括削除し、Ctrl+Z用のUndo履歴も記録する
        // Delete the whole selection and record the undo history for Ctrl+Z
        public void CommitDelete()
        {
            if (_canceled) return;

            // 削除送信後に端点が消え得るため撤去物を先に記録する
            // Record removed objects before sends can remove their endpoints
            var committed = new List<IDeleteTarget>();
            foreach (var target in _selectedTargets.Values)
            {
                if (target is Object unityTarget && unityTarget == null)
                {
                    Debug.LogWarning("[DragDelete] selected target was destroyed before commit");
                    continue;
                }
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
            AimFilter = DeleteAimFilter.Frontmost;

            // Ctrl+Z用のUndo履歴を記録（空バッチはPushしない）
            // Record the undo history for Ctrl+Z (skip empty batches)
            if (record.HasRemovedObjects) _buildOperationHistory.Push(record);
        }

        // キャンセルされていない場合のみ削除確定を許可する
        // Allow commit only when the selection has not been canceled
        public bool CanCommit()
        {
            return !_canceled;
        }

        // ドラッグでなぞった対象が1件以上あるか（ESCで取り消せる選択が存在するか）
        // Whether at least one target is selected (an active selection that ESC can cancel)
        public bool HasSelection()
        {
            return _selectedTargets.Count > 0;
        }
    }
}
