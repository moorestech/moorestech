using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.GrabOverlay
{
    /// <summary>
    /// 掴み絵が追うポインタ（画面 px は左下原点）とパネル座標への換算
    /// Pointer the grab picture follows (screen px, bottom-left origin) and its conversion to panel coordinates
    /// </summary>
    public interface IGrabOverlayPointer
    {
        IObservable<Vector2> OnPointerMovedOnScreen { get; }
        Vector2 CurrentPointerOnScreen();
        Vector2 ScreenToPanel(IPanel panel, Vector2 pointerOnScreen);
    }
}
