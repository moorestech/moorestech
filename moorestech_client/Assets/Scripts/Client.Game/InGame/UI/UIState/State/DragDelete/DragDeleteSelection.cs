using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    /// <summary>
    ///     ドラッグ中に選択された削除対象を管理するモデルクラス。削除確定時のUndo履歴記録も担う
    ///     Model class that manages delete targets selected during a drag; also records undo history on commit
    /// </summary>
    public class DragDeleteSelection
    {
        private readonly BuildOperationHistory _buildOperationHistory;

        // 論理削除キーで重複排除する（同一機械の複数メッシュ子などを1件に集約）
        // Dedupe by logical delete key so multiple mesh children of one machine collapse into one
        private readonly Dictionary<object, IDeleteTarget> _selectedTargets = new();
        private bool _canceled;

        // 最初に選択したブロックの破壊カテゴリーをセッションのカテゴリーとして固定する（未選択時はnull）
        // Fix the first selected block's destruction category as the session category (null while empty)
        private string _sessionCategory;

        // 別カテゴリー混在時の拒否理由。IsRemovableのreasonと同様に生文字列で表示する
        // Deny reason shown when mixing categories; a raw string like IsRemovable's reason
        public const string DifferentCategoryDenyReason = "別カテゴリーのブロックは同時に選択できません。";

        public DragDeleteSelection(BuildOperationHistory buildOperationHistory)
        {
            _buildOperationHistory = buildOperationHistory;
        }

        // 新しいドラッグ開始時に選択・キャンセル状態・セッションカテゴリーをリセットする
        // Reset selection, canceled state, and session category when a new drag begins
        public void BeginDrag()
        {
            _selectedTargets.Clear();
            _canceled = false;
            _sessionCategory = null;
        }

        // 対象を選択へ追加する。削除可否・カテゴリー整合をまとめて判定し、追加不可なら拒否理由を返す
        // Add a target to the selection; judges removability and category together, returning a deny reason when rejected
        public bool TryAddTarget(IDeleteTarget target, out string denyReason)
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
                denyReason = DifferentCategoryDenyReason;
                return false;
            }

            // 既に同じ論理対象が選択済みなら重複追加しない（拒否理由なしの成功扱い）
            // Skip when already selected: success without a deny reason (prevents duplicate Delete)
            var key = target.GetDeleteTargetKey();
            if (_selectedTargets.ContainsKey(key)) return true;

            // 最初の追加でセッションカテゴリーを固定する
            // Fix the session category on the first added target
            _sessionCategory ??= target.GetDestructionCategory();

            _selectedTargets.Add(key, target);
            target.SetRemovePreviewing();
            return true;
        }

        // このセッションに追加可能なカテゴリーか（未選択時は何でも可、以降は同一カテゴリーのみ）
        // Whether the target's category can join this session (anything while empty, then same category only)
        private bool IsCategoryCompatible(IDeleteTarget target)
        {
            if (_sessionCategory == null) return true;
            return _sessionCategory == target.GetDestructionCategory();
        }

        // 選択を全てリセットしてキャンセル状態にする（ESC操作）
        // Reset all selections and mark as canceled (ESC behavior)
        public void CancelSelection()
        {
            foreach (var target in _selectedTargets.Values) target.ResetMaterial();

            _selectedTargets.Clear();
            _canceled = true;
            _sessionCategory = null;
        }

        // 選択を一括削除し、Ctrl+Z用のUndo履歴も記録する
        // Delete the whole selection and record the undo history for Ctrl+Z
        public void CommitDelete()
        {
            if (_canceled) return;

            var committed = new List<IDeleteTarget>(_selectedTargets.Values);
            foreach (var target in committed)
            {
                // Delete はサーバー往復の非同期なので即座に赤プレビューだけ戻す
                // Delete is async over the server, so we just clear the red preview immediately
                target.Delete();
                target.ResetMaterial();
            }

            _selectedTargets.Clear();
            _sessionCategory = null;

            RecordRemoveOperation(committed);

            #region Internal

            void RecordRemoveOperation(List<IDeleteTarget> targets)
            {
                // ブロック対象だけを楽観的記録（撤去失敗セルはUndo時の空き座標ガードで自然に無効化）
                // Optimistically record block targets only (failed removals are neutralized by the empty-cell guard on undo)
                var removedBlocks = new List<RemovedBlockInfo>();
                foreach (var target in targets)
                {
                    if (target is not BlockGameObjectChild blockChild) continue;
                    var blockGameObject = blockChild.BlockGameObject;
                    removedBlocks.Add(new RemovedBlockInfo(
                        blockGameObject.BlockPosInfo.OriginalPos,
                        blockGameObject.BlockId,
                        blockGameObject.BlockPosInfo.BlockDirection));
                }
                if (removedBlocks.Count != 0) _buildOperationHistory.Push(new RemoveOperationRecord(removedBlocks));
            }

            #endregion
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
