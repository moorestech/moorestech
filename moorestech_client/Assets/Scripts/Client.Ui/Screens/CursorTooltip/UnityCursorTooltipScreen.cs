using System;
using Client.Input;
using UniRx;
using UnityEngine;

namespace Client.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// 本番の実画面。ロック中は中央を指す
    /// Production screen: anchors to the centre while locked
    /// </summary>
    public sealed class UnityCursorTooltipScreen : ICursorTooltipScreen
    {
        // ロックの唯一の書き手 InputManager.MouseCursorVisible の通知。書き換えた後に流れるので、AnchorOnScreen の読みは新しい状態になる
        // Notified by InputManager.MouseCursorVisible, the only lock writer; it fires after writing, so AnchorOnScreen reads the new state
        public IObservable<Unit> OnAnchorModeChanged => InputManager.OnCursorLockChanged.AsUnitObservable();

        public Vector2 AnchorOnScreen(Vector2 latestPointerOnScreen)
        {
            // ロックは InputManager の値を読む
            // Read the lock from its writer InputManager (same as the UiPointerHitTest precedent)
            return CursorTooltipScreenAnchor.AnchorOnScreen(InputManager.IsCursorLocked, latestPointerOnScreen, ScreenCenter.GetPosition());
        }
    }
}
