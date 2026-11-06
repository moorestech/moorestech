using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// 実画面への問い合わせの唯一の口
    /// The only gateway to the real screen
    /// </summary>
    public interface ICursorTooltipScreen
    {
        // ロック中は中央、それ以外は渡されたポインタ
        // Centre while locked; otherwise the given pointer
        Vector2 ResolveAnchorInPanel(IPanel panel, Vector2 latestPointerInPanel);

        // 今のポインタのパネル座標（View が載った時の初期値）
        // The current pointer in panel coordinates (the initial value when the view attaches)
        Vector2 CurrentPointerInPanel(IPanel panel);

        // ポインタ移動をパネル座標で流す
        // Publishes pointer moves in panel coordinates
        IObservable<Vector2> OnPointerMovedInPanel(IPanel panel);

        // 実画面 px / パネル単位
        // Screen pixels per panel unit
        float ScreenPixelsPerPanelPixel(IPanel panel);
    }
}
