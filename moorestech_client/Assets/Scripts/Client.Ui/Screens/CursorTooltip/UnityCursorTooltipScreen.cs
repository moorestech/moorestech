using System;
using Client.Input;
using Client.Input.Pointer;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// 本番の実画面。ロック中は中央、倍率は scaledPixelsPerPoint
    /// Production screen: centre while locked, scale is scaledPixelsPerPoint
    /// </summary>
    public sealed class UnityCursorTooltipScreen : ICursorTooltipScreen
    {
        private readonly InputSystemPointerPositionStream _pointerPositionStream;

        public UnityCursorTooltipScreen(InputSystemPointerPositionStream pointerPositionStream)
        {
            _pointerPositionStream = pointerPositionStream;
        }

        public IObservable<Vector2> OnPointerMovedOnScreen => _pointerPositionStream.OnPointerScreenPositionChanged;

        // ロックの唯一の書き手 InputManager.MouseCursorVisible の通知。書き換えた後に流れるので、Resolve の読みは新しい状態になる
        // Notified by InputManager.MouseCursorVisible, the only lock writer; it fires after writing, so Resolve reads the new state
        public IObservable<Unit> OnAnchorModeChanged => InputManager.OnCursorLockChanged.AsUnitObservable();

        public Vector2 ResolveAnchorInPanel(IPanel panel, Vector2 latestPointerOnScreen)
        {
            // 前例 UiPointerHitTest と同じく lockState を読み、いまの画面寸法と倍率で換算する
            // Read lockState like the UiPointerHitTest precedent and convert with the current screen size and scale
            var cursorLocked = UnityEngine.Cursor.lockState == CursorLockMode.Locked;
            var topLeftScreenPosition = CursorTooltipScreenAnchor.TopLeftScreenPosition(cursorLocked, latestPointerOnScreen, ScreenCenter.GetPosition(), Screen.height);
            return RuntimePanelUtils.ScreenToPanel(panel, topLeftScreenPosition);
        }

        public Vector2 CurrentPointerOnScreen()
        {
            var mouse = HybridInput.GetMousePosition();
            return new Vector2(mouse.x, mouse.y);
        }

        public float ScreenPixelsPerPanelPixel(IPanel panel)
        {
            return panel.scaledPixelsPerPoint;
        }
    }
}
