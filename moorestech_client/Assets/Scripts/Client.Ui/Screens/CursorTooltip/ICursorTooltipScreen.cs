using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// 実画面への問い合わせの唯一の口。ロックの分岐と画面 px→パネル座標の換算はここだけが持つ
    /// The only gateway to the real screen; it alone owns the lock branch and the screen px to panel conversion
    /// </summary>
    public interface ICursorTooltipScreen
    {
        // ロック中は画面中央をパネル座標へ換算した値、それ以外は渡されたポインタ
        // While locked, the screen centre converted to panel coordinates; otherwise the given pointer
        Vector2 ResolveAnchorInPanel(IPanel panel, Vector2 latestPointerInPanel);

        // 今のポインタのパネル座標（View が載った時の初期値）
        // The current pointer in panel coordinates (the initial value when the view attaches)
        Vector2 CurrentPointerInPanel(IPanel panel);

        // ポインタが動くたびにパネル座標で流す（何も拾わない所でも流れる）
        // Publishes the pointer in panel coordinates on every move, even where nothing is pickable
        IObservable<Vector2> OnPointerMovedInPanel(IPanel panel);

        // 実画面 px / パネル単位
        // Screen pixels per panel unit
        float ScreenPixelsPerPanelPixel(IPanel panel);
    }
}
