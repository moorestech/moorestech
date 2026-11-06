using System;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// 実画面への問い合わせの唯一の口。ポインタは実画面 px で持ち、置く時にいまの倍率でパネル座標へ直す
    /// The only gateway to the real screen; pointers stay in screen px and are converted with the current scale when placing
    /// </summary>
    public interface ICursorTooltipScreen
    {
        // ロック中は中央、それ以外は渡されたポインタを、いまの倍率でパネル座標へ直す
        // Centre while locked, otherwise the given pointer, converted to panel coordinates with the current scale
        Vector2 ResolveAnchorInPanel(IPanel panel, Vector2 latestPointerOnScreen);

        // 今のポインタの実画面 px（View が載った時の初期値）
        // The current pointer in screen px (the initial value when the view attaches)
        Vector2 CurrentPointerOnScreen();

        // ポインタ移動を実画面 px で流す
        // Publishes pointer moves in screen px
        IObservable<Vector2> OnPointerMovedOnScreen { get; }

        // 指す点の決め方（ロック↔解除）が変わった時に流す
        // Publishes when the anchoring mode (locked vs unlocked) changes
        IObservable<Unit> OnAnchorModeChanged { get; }

        // 実画面 px / パネル単位
        // Screen pixels per panel unit
        float ScreenPixelsPerPanelPixel(IPanel panel);
    }
}
