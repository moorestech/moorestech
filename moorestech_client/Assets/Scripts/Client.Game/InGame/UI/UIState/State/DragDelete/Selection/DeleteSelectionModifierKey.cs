using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Selection
{
    /// <summary>
    ///     なぞり・箱を選択からの除外にする修飾キー（Ctrl／Mac の Command。Ctrl+Z の判定と同じキー）
    ///     Modifier that turns a stroke or box into deselection (Ctrl, or Command on Mac; same keys as the Ctrl+Z check)
    /// </summary>
    internal static class DeleteSelectionModifierKey
    {
        internal static DeleteSelectionIntent ReadIntent()
        {
            var isDeselectHeld = HybridInput.GetKey(KeyCode.LeftControl) || HybridInput.GetKey(KeyCode.LeftCommand);
            return isDeselectHeld ? DeleteSelectionIntent.Remove : DeleteSelectionIntent.Add;
        }
    }
}
