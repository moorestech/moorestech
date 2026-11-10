using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Client.Ui.Foundation.Composite
{
    /// <summary>
    /// 世界の色の拡大を URP の final blit に揃える
    /// Matches the world colour upscale to URP's final blit
    /// </summary>
    /// <remarks>
    /// URP は描画解像度を下げたとき、選んだ拡大フィルタ（Auto は「1/renderScale と画面の寸法が整数で割り切れれば Point、それ以外 Linear」）で final blit を読む。合成も同じ点か双線形で読まないと、UI の下の世界の色が細部（地平線など）で URP とずれる
    /// 解決後のフィルタ（cameraData.upscalingFilter）は internal なので、URP の ResolveUpscalingFilterSelection と同じ式を公開の値（renderScale・カメラの画素数・asset の upscalingFilter）で写す
    /// FSR・STP は合成の 1 枚のシェーダでは写せないので双線形で読み、ずれることを 1 回だけエラーで知らせる（品質の段の asset はどれも Auto）
    /// When the render scale is reduced URP reads the final blit with the selected filter (Auto: Point when 1/renderScale and the screen size divide evenly, Linear otherwise); the composite must read with the same point or bilinear filter, or the world under the UI drifts from URP at fine detail such as the horizon
    /// The resolved filter (cameraData.upscalingFilter) is internal, so this copies URP's ResolveUpscalingFilterSelection using public values (renderScale, camera pixel size, the asset's upscalingFilter)
    /// FSR and STP cannot be reproduced by the single composite shader, so they read bilinearly and report the mismatch once as an error (every quality tier asset uses Auto)
    /// </remarks>
    internal static class UiSrgbWorldUpscaleFilter
    {
        private static bool _reportedUnsupportedFilter;

        public static bool UsesPointSampling(float renderScale, int pixelWidth, int pixelHeight, UpscalingFilterSelection selection)
        {
            // 拡大しないときは中間色と画面が同じ大きさで、どちらで読んでも同じ
            // Without upscaling the intermediate matches the screen, so either filter reads the same
            if (renderScale >= 1f) return false;
            switch (selection)
            {
                case UpscalingFilterSelection.Point:
                    return true;
                case UpscalingFilterSelection.Linear:
                    return false;
                case UpscalingFilterSelection.Auto:
                    return IsIntegerScale();
                default:
                    ReportUnsupported();
                    return false;
            }

            #region Internal

            bool IsIntegerScale()
            {
                var pixelScale = 1f / renderScale;
                if (!Mathf.Approximately(pixelScale - Mathf.Floor(pixelScale), 0f)) return false;
                var widthScale = pixelWidth / pixelScale;
                var heightScale = pixelHeight / pixelScale;
                return Mathf.Approximately(widthScale - Mathf.Floor(widthScale), 0f) && Mathf.Approximately(heightScale - Mathf.Floor(heightScale), 0f);
            }

            void ReportUnsupported()
            {
                if (_reportedUnsupportedFilter) return;
                _reportedUnsupportedFilter = true;
                Debug.LogError($"[UiSrgbWorldUpscaleFilter] URP の拡大フィルタ {selection} は合成で写せないので、UI の下の世界の色は双線形で読む（URP とずれる）");
            }

            #endregion
        }
    }
}
