using System;
using UniRx;
using UnityEngine;

namespace Client.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// ツールチップの指す点を決める口
    /// Decides the point the tooltip anchors to; the pointer source and conversion belong to Foundation IUiScreenPointer, the scale to StageScaleSource
    /// </summary>
    public interface ICursorTooltipScreen
    {
        // ロック中は中央、他はポインタの点
        // The screen centre while locked, otherwise the given pointer (both in screen px, bottom-left origin)
        Vector2 AnchorOnScreen(Vector2 latestPointerOnScreen);

        // 指す点の決め方（ロック↔解除）が変わった時に流す
        // Publishes when the anchoring mode (locked vs unlocked) changes
        IObservable<Unit> OnAnchorModeChanged { get; }
    }
}
