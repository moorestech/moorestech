using Client.Game.InGame.Control;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session
{
    /// <summary>
    ///     2クリック設置が読むキー。R=向き/分岐の左右、C=同コスト候補の送り、Ctrl=分岐、左クリック=始点・終点の確定
    ///     Keys read by two-click placement: R rotates or picks the branch side, C cycles equal-cost candidates, Ctrl branches, left click fixes start and end
    /// </summary>
    public static class BeltRouteKeyInput
    {
        public static bool IsRotatePressed => InputManager.Playable.BlockPlaceRotation.GetKeyDown;

        public static bool IsCyclePriorityPressed => HybridInput.GetKeyDown(KeyCode.C);

        // Undo（Ctrl+Z）と同じ修飾キーの読み方に揃える
        // Reads the modifier the same way as undo (Ctrl+Z)
        public static bool IsBranchModifierHeld => HybridInput.GetKey(KeyCode.LeftControl) || HybridInput.GetKey(KeyCode.LeftCommand);

        // UI上の押下は世界への操作にしない
        // A press over UI never acts on the world
        public static bool IsConfirmPressed => InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi();
    }
}
