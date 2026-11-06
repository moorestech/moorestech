using System;
using Client.Input;
using Client.Input.Pointer;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// 本番の実画面。ロック中は ScreenCenter、そうでなければ届いたポインタを指し、倍率はパネルの scaledPixelsPerPoint（設計書 [A-41]）
    /// Production screen: points at ScreenCenter while locked, otherwise at the latest pointer; scale is the panel's scaledPixelsPerPoint (design [A-41])
    /// </summary>
    public sealed class UnityCursorTooltipScreen : ICursorTooltipScreen
    {
        private readonly IPointerPositionStream _pointerPositionStream;

        public UnityCursorTooltipScreen(IPointerPositionStream pointerPositionStream)
        {
            _pointerPositionStream = pointerPositionStream;
        }

        public Vector2 ResolveAnchorInPanel(IPanel panel, Vector2 latestPointerInPanel)
        {
            // ロック中はポインタが凍るので、クロスヘアと同じ画面中央を指す。ロックの唯一の書き手は InputManager.MouseCursorVisible（前例 UiPointerHitTest と同じ読み方）
            // The pointer freezes while locked, so point at the screen centre like the crosshair; the only lock writer is InputManager.MouseCursorVisible (same read as UiPointerHitTest)
            if (UnityEngine.Cursor.lockState != CursorLockMode.Locked) return latestPointerInPanel;
            return ScreenToPanel(panel, ScreenCenter.GetPosition());
        }

        public Vector2 CurrentPointerInPanel(IPanel panel)
        {
            var mouse = HybridInput.GetMousePosition();
            return ScreenToPanel(panel, new Vector2(mouse.x, mouse.y));
        }

        public IObservable<Vector2> OnPointerMovedInPanel(IPanel panel)
        {
            return _pointerPositionStream.OnPointerScreenPositionChanged.Select(screenPosition => ScreenToPanel(panel, screenPosition));
        }

        public float ScreenPixelsPerPanelPixel(IPanel panel)
        {
            return panel.scaledPixelsPerPoint;
        }

        // 画面 px（左下原点）を上原点へ直してパネル座標へ変える（Foundation/Input/GameUiPointerProbe と同じ換算。本PR外の提案で 1 本へ寄せる）
        // Flip screen pixels (bottom-left origin) to top origin, then convert to panel coordinates (same as Foundation/Input/GameUiPointerProbe; merging is an out-of-PR proposal)
        private static Vector2 ScreenToPanel(IPanel panel, Vector2 screenPosition)
        {
            return RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        }
    }
}
