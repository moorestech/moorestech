using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Client.Ui.Foundation.Composite
{
    /// <summary>
    /// 世界の色の拡大を URP の final blit に揃える
    /// Matches the world colour upscale to URP's final blit
    /// </summary>
    /// <remarks>
    /// URP 17.6（Render Graph）では、合成パスがあるとポスト処理の結果は双線形の中間テクスチャに入り、final blit はそれを双線形で拡大する。点で読むのは最終ポスト処理パス（FXAA・TAA の CAS・FSR）が拡大を受け持つときだけで、そこでは選んだ拡大フィルタ（Auto は「1/renderScale と画面の寸法が整数で割り切れれば Point、それ以外 Linear」）を使う
    /// 解決後のフィルタ（cameraData.upscalingFilter）は internal なので、URP の ResolveUpscalingFilterSelection と同じ式を公開の値で写す。STP はポスト処理の中で画面の解像度まで拡大し終えるので拡大は残らない。対応環境の FSR は合成の 1 枚のシェーダでは写せないので双線形で読み、ずれることを 1 回だけエラーで知らせる（品質の段の asset はどれも Auto）
    /// In URP 17.6 (Render Graph) the composite pass makes the post-processed result land in a bilinear intermediate, which the final blit upscales bilinearly; point reads happen only when the final post-processing pass (FXAA, TAA's CAS, FSR) does the upscale with the selected filter (Auto: Point when 1/renderScale and the screen size divide evenly, Linear otherwise)
    /// The resolved filter (cameraData.upscalingFilter) is internal, so this copies URP's ResolveUpscalingFilterSelection with public values; STP finishes upscaling to screen resolution inside post-processing, and FSR on supported hardware cannot be reproduced by the single composite shader, so it reads bilinearly and reports the mismatch once as an error (every quality tier asset uses Auto)
    /// </remarks>
    internal static class UiSrgbWorldUpscaleFilter
    {
        private static bool _reportedUnsupportedFilter;

        public static bool UsesPointSampling(bool postProcessEnabled, bool finalPostPassRuns, float renderScale, int pixelWidth, int pixelHeight, UpscalingFilterSelection selection)
        {
            // 最終ポスト処理パスが無いと、URP は双線形の中間テクスチャを final blit で双線形に拡大する
            // Without the final post-processing pass URP upscales the bilinear intermediate bilinearly in the final blit
            if (!postProcessEnabled || !finalPostPassRuns) return false;
            // 拡大しないときは中間色と画面が同じ大きさで、どちらで読んでも同じ
            // Without upscaling the intermediate matches the screen, so either filter reads the same
            if (1f <= renderScale) return false;
            // STP はポスト処理の中で画面の解像度まで拡大し終える。非対応環境の FSR は Auto へ戻る
            // STP has already upscaled to screen resolution inside post-processing; FSR falls back to Auto when unsupported
            if (selection == UpscalingFilterSelection.STP) return false;
            if (selection == UpscalingFilterSelection.FSR && !FSRUtils.IsSupported()) selection = UpscalingFilterSelection.Auto;
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
