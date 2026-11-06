using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Stage.ScaleReading
{
    /// <summary>
    /// 舞台の倍率（パネル 1px が実画面の何 px か）を読む唯一の式。式は C3-b の実測で選んだ（台帳「舞台の倍率の読み口」）
    /// The single formula reading the stage scale (real-screen px per panel px); chosen by the C3-b measurement (ledger row on the stage scale)
    /// </summary>
    public static class StageScaleMeasure
    {
        public static bool TryRead(IPanel panel, out float scale)
        {
            // PanelSettings（ScaleWithScreenSize・Expand）の倍率と一致した値をそのまま使う
            // Use the value that matched the PanelSettings scale (ScaleWithScreenSize, Expand)
            scale = panel.scaledPixelsPerPoint;
            return scale > 0f;
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
