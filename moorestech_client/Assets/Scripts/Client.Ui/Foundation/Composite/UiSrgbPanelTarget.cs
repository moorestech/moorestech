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
            SyncToScreenSize();
        }

        // 画面の大きさが変わったら RT を作り直す（Unity に画面の大きさの変化通知は無い）
        // Recreate the RT when the screen size changed (Unity has no screen-size change notification)
        public void SyncToScreenSize()
        {
            // silent-ok: 最小化中は描く物が無く前の RT を使い続けてよい / 復帰時に通常経路で作り直す
            if (Screen.width <= 0 || Screen.height <= 0) return;
            if (_texture != null && _texture.width == Screen.width && _texture.height == Screen.height) return;
            var previous = _texture;
            _texture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { name = "GameUiSrgbPanel" };
            if (!_texture.Create()) throw new InvalidOperationException($"[UiSrgbPanelTarget] failed to create the {Screen.width}x{Screen.height} panel RT");
            _settings.targetTexture = _texture;
            UiSrgbCompositeSource.SetTexture(_texture);
            if (previous != null) Release(previous);
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
