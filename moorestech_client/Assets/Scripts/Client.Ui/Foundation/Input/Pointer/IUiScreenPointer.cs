using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input.Pointer
{
    /// <summary>
    /// 画面を追う UI が読むポインタ（実画面 px・左下原点）とパネル座標への換算の唯一の口
    /// The only port for the pointer that screen-following UI reads (screen px, bottom-left origin) and its conversion to panel coordinates
    /// </summary>
    public interface IUiScreenPointer
    {
        // InputSystem のイベントと同じフレームで流す（PointerMoveEvent は拾わない所で届かないため）
        // Publishes in the same frame as InputSystem events (PointerMoveEvent misses non-pickable areas)
        IObservable<Vector2> OnPointerMovedOnScreen { get; }

        Vector2 CurrentPointerOnScreen();

        Vector2 ScreenToPanel(IPanel panel, Vector2 bottomLeftScreenPosition);
    }
}
