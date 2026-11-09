using Client.Game.InGame.Control;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session
{
    /// <summary>
    ///     2クリック設置が読むキー。R: 向き/分岐の出口の切替、Ctrl: 分岐、左クリック: 始点・終点の確定
    ///     Keys read by two-click placement. R: rotate / switch the branch exit, Ctrl: branch, Left click: fix start and end
    /// </summary>
    public static class BeltRouteKeyInput
    {
        public static bool IsRotatePressed => InputManager.Playable.BlockPlaceRotation.GetKeyDown;

        // Undo（Ctrl+Z）と同じ修飾キーの読み方に揃える
        // Reads the modifier the same way as undo (Ctrl+Z)
        public static bool IsBranchModifierHeld => HybridInput.GetKey(KeyCode.LeftControl) || HybridInput.GetKey(KeyCode.LeftCommand);

        // UI上の押下は世界への操作にしない
        // A press over UI never acts on the world
        public static bool IsConfirmPressed => InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi();
    }
}
