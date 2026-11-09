using System;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Client.Ui.Foundation.Composite
{
    /// <summary>
    /// GameUi のパネルを画面と同じ大きさの RT（sRGB 変換なし）へ sRGB の値のまま描かせ、URP の差し込み口へ渡す（A-180）
    /// Makes the GameUi panel draw, blending as sRGB values, into a screen-sized RT without sRGB conversion and hands it to the URP hook (A-180)
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
            // sRGB の値のまま合成する設定がアセットから外れていたら起動で止める（黙って Linear 合成に戻るのを防ぐ）
            // Stop at startup when the asset lost the sRGB-value blending setting (prevents silently falling back to Linear blending)
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
            if (_texture != null && _texture.width == Screen.width && _texture.height == Screen.height) return;
            var previous = _texture;
            _texture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { name = "GameUiSrgbPanel" };
            _texture.Create();
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
