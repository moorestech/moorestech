using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Stage.ScaleReading
{
    /// <summary>
    /// 舞台の倍率を読む唯一の式
    /// The only formula that reads the stage scale
    /// </summary>
    internal static class StageScaleMeasure
    {
        public static bool TryRead(IPanel panel, out float scale)
        {
            // PanelSettings（ScaleWithScreenSize・Expand）の倍率と一致した値をそのまま使う
            // Use the value that matched the PanelSettings scale (ScaleWithScreenSize, Expand)
            scale = panel.scaledPixelsPerPoint;
            return 0f < scale;
        }

        // 押下の時点で 1 回だけ読む口（DI の外で new される UiPressGesture 用）。読めなければ開発者へ知らせ 1 で続ける
        // One-shot read at press time (for UiPressGesture, built outside DI); if unreadable, tell the developer and continue with 1
        public static float ReadOrUnit(IPanel panel, string reader)
        {
            if (TryRead(panel, out var scale)) return scale;
            Debug.LogError($"[StageScale] stage scale is unreadable; {reader} continues with scale 1");
            return 1f;
        }
    }
}
