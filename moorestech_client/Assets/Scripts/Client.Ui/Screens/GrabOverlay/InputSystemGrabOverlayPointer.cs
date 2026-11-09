using System;
using Client.Input;
using Client.Input.Pointer;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.GrabOverlay
{
    /// <summary>
    /// 本番のポインタ。InputSystem のイベントと同じフレームで流し、左上原点へ直してパネル座標へ換算する
    /// 換算は UnityCursorTooltipScreen と同じ中身で、Foundation/Input への共通化は integrator へ依頼中（bd moorestech-albof.40）
    /// Production pointer; publishes in the same frame as InputSystem events and converts to panel coordinates via a top-left origin
    /// The conversion duplicates UnityCursorTooltipScreen; sharing it in Foundation/Input is requested from the integrator (bd moorestech-albof.40)
    /// </summary>
    public sealed class InputSystemGrabOverlayPointer : IGrabOverlayPointer
    {
        private readonly InputSystemPointerPositionStream _positions;

        public InputSystemGrabOverlayPointer(InputSystemPointerPositionStream positions)
        {
            _positions = positions;
        }

        public IObservable<Vector2> OnPointerMovedOnScreen => _positions.OnPointerScreenPositionChanged;

        public Vector2 CurrentPointerOnScreen()
        {
            var mouse = HybridInput.GetMousePosition();
            return new Vector2(mouse.x, mouse.y);
        }

        public Vector2 ScreenToPanel(IPanel panel, Vector2 pointerOnScreen)
        {
            // RuntimePanelUtils は左上原点の画面 px を受ける（前例 UnityCursorTooltipScreen.ResolveAnchorInPanel）
            // RuntimePanelUtils takes top-left-origin screen px (precedent UnityCursorTooltipScreen.ResolveAnchorInPanel)
            return RuntimePanelUtils.ScreenToPanel(panel, new Vector2(pointerOnScreen.x, Screen.height - pointerOnScreen.y));
        }
    }
}
