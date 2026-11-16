using System;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Client.Ui.Foundation.Composite
{
    /// <summary>
    /// パネルを sRGB 変換なしの RT へ描き渡す
    /// Draws the panel into an RT without sRGB conversion and hands it over
    /// </summary>
    /// <remarks>
    /// PanelSettings は複製に当てる（本番のアセットを実行時に書き換えない。前例 RenderPipelineQualityApplier）
    /// The PanelSettings change goes to a clone (never rewrite the production asset at runtime; precedent RenderPipelineQualityApplier)
    /// </remarks>
    internal sealed class UiSrgbPanelTarget
    {
        private readonly PanelSettings _settings;
        private RenderTexture _texture;
        private int _failedWidth;
        private int _failedHeight;

        public UiSrgbPanelTarget(UIDocument document)
        {
            // sRGB の値のまま合成する設定がアセットから外れていたら生成時に例外で知らせる（黙って Linear 合成に戻るのを防ぐ）
            // Throw at construction when the asset lost the sRGB-value blending setting (prevents silently falling back to Linear blending)
            var source = document.panelSettings;
            if (!source.forceGammaRendering) throw new InvalidOperationException($"[UiSrgbPanelTarget] {source.name} must have forceGammaRendering on");
            _settings = Object.Instantiate(source);
            _settings.name = source.name;
            _settings.clearColor = true;
            _settings.colorClearValue = Color.clear;
            document.panelSettings = _settings;

            // 初回に作れなければ起動の失敗として投げる（S5-gate D-18）
            // Failing the first creation throws as a boot failure (S5-gate D-18)
            if (Resize(Screen.width, Screen.height) == UiSrgbPanelResizeResult.CreationFailed) throw new UiSrgbPanelRenderTextureUnavailableException(Screen.width, Screen.height);
        }

        // 画面の大きさが変わったら RT を作り直す。作れなくても投げない（Tick の例外は VContainer の Tick ループごと止めるため）
        // Recreate the RT when the screen size changed; never throws (an exception from Tick stops VContainer's whole Tick loop)
        internal UiSrgbPanelResizeResult SyncToSize(int width, int height)
        {
            // silent-ok: 同じ大きさの失敗は前回 LogError 済みで、毎フレーム Create を叩くと Unity のエラーが溢れる / 開発者は最初の LogError で気づき、大きさが変われば再び試す
            if (0 < width && width == _failedWidth && height == _failedHeight) return UiSrgbPanelResizeResult.SkippedFailedSize;

            var result = Resize(width, height);
            if (result == UiSrgbPanelResizeResult.CreationFailed) Debug.LogError($"[UiSrgbPanelTarget] failed to create the {width}x{height} panel RT; keeping the previous RT and retrying when the screen size changes");
            return result;
        }

        private UiSrgbPanelResizeResult Resize(int width, int height)
        {
            // 失敗の記録は直前に試した大きさの分だけ持つ（大きさが変わればまた試す）
            // The failure record covers only the size tried last (a changed size is tried again)
            _failedWidth = 0;
            _failedHeight = 0;

            // silent-ok: 最小化中は描く物が無く前の RT を使い続けてよい / 復帰時に通常経路で作り直す
            if (width <= 0 || height <= 0) return UiSrgbPanelResizeResult.Unchanged;
            if (_texture != null && _texture.width == width && _texture.height == height) return UiSrgbPanelResizeResult.Unchanged;

            // 作れなかったら前の RT のまま、失敗した大きさを覚えて返す
            // On failure keep the previous RT and remember the failed size
            var candidate = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { name = "GameUiSrgbPanel" };
            if (!candidate.Create())
            {
                Release(candidate);
                _failedWidth = width;
                _failedHeight = height;
                return UiSrgbPanelResizeResult.CreationFailed;
            }

            // 作れたときだけ差し替えて前を捨てる
            // Swap only on success and drop the previous one
            var previous = _texture;
            _texture = candidate;
            _settings.targetTexture = _texture;
            UiSrgbCompositeSource.SetTexture(_texture);
            if (previous != null) Release(previous);
            return UiSrgbPanelResizeResult.Recreated;
        }

        public void Dispose()
        {
            if (_texture != null) Release(_texture);
            _texture = null;
            Object.Destroy(_settings);
        }

        private static void Release(RenderTexture texture)
        {
            UiSrgbCompositeSource.ClearTexture(texture);
            texture.Release();
            Object.Destroy(texture);
        }
    }
}
