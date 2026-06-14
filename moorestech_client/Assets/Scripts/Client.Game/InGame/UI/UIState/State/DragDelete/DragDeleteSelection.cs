using System.Collections.Generic;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    /// <summary>
    ///     ドラッグ中に選択された削除対象を管理する純粋なモデルクラス
    ///     Pure model class that manages delete targets selected during a drag
    /// </summary>
    public class DragDeleteSelection
    {
        private readonly HashSet<IDeleteTarget> _selectedTargets = new();
        private bool _canceled;

        // 新しいドラッグ開始時に選択とキャンセル状態をリセットする
        // Reset selection and canceled state when a new drag begins
        public void BeginDrag()
        {
            _selectedTargets.Clear();
            _canceled = false;
        }

        // 削除可能で未選択の対象を追加し、プレビュー表示する
        // Add a removable, not-yet-selected target and show its preview
        public void AddTarget(IDeleteTarget target)
        {
            if (_canceled) return;
            if (target == null) return;
            if (!target.IsRemovable(out _)) return;
            if (!_selectedTargets.Add(target)) return;

            target.SetRemovePreviewing();
        }

        // 選択を全てリセットしてキャンセル状態にする（ESC操作）
        // Reset all selections and mark as canceled (ESC behavior)
        public void CancelSelection()
        {
            foreach (var target in _selectedTargets) target.ResetMaterial();

            _selectedTargets.Clear();
            _canceled = true;
        }

        // 選択対象を全て削除しMaterialを戻して選択を空にする（マウス離し操作）
        // Delete all selected targets, reset materials, then clear selection (mouse release)
        public void CommitDelete()
        {
            if (_canceled) return;

            foreach (var target in _selectedTargets)
            {
                // Delete はサーバー往復の非同期なので即座に赤プレビューだけ戻す
                // Delete is async over the server, so we just clear the red preview immediately
                target.Delete();
                target.ResetMaterial();
            }

            _selectedTargets.Clear();
        }

        // キャンセルされていない場合のみ削除確定を許可する
        // Allow commit only when the selection has not been canceled
        public bool CanCommit()
        {
            return !_canceled;
        }

        // 現在選択中の対象数を返す
        // Return the number of currently selected targets
        public int SelectedCount()
        {
            return _selectedTargets.Count;
        }
    }
}
