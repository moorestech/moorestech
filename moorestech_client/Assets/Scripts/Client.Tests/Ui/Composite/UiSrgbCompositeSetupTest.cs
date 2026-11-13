using System.Linq;
using Client.Ui.Editor.Capture;
using Client.Ui.Foundation.Composite;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Composite
{
    /// <summary>
    /// 半透明を Web と同じ sRGB 空間で合成する設定と、その合成が Web の式と一致すること（L1。A-180）
    /// The setup that blends translucency in sRGB space like the Web, and that the blend matches the Web formula (L1, A-180)
    /// </summary>
    public class UiSrgbCompositeSetupTest
    {
        private const string PanelSettingsPath = "Assets/UI Toolkit/GameUi/GameUiPanelSettings.asset";
        private static readonly string[] RendererDataPaths = { "Assets/Asset/Common/URPSettings/URP Data.asset", "Assets/Asset/Common/URPSettings/URP Data - Skit.asset" };

        [Test]
        public void 本番のPanelSettingsはsRGBの値のまま描く()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            Assert.IsTrue(settings.forceGammaRendering, PanelSettingsPath);
        }

        [Test]
        public void 両方のURPレンダラーに合成の差し込み口とシェーダが載っている()
        {
            foreach (var path in RendererDataPaths)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                var features = data.rendererFeatures.OfType<UiSrgbCompositeFeature>().ToList();
                Assert.AreEqual(1, features.Count, path);
                Assert.IsTrue(features[0].isActive, path);
                var shader = new SerializedObject(features[0]).FindProperty("compositeShader").objectReferenceValue as Shader;
                Assert.IsNotNull(shader, path);
                Assert.IsTrue(shader.isSupported, path);
            }
        }

        // UI の面の上の半透明は Web の α のまま描いて、地との sRGB の over（Chrome と同じ式）と ±1 で一致する（世界の上は UiSrgbCompositeShaderTest が受け持つ）
        // Translucency over a UI surface drawn with the Web alpha matches the sRGB over (Chrome's formula) within 1 (over the world is covered by UiSrgbCompositeShaderTest)
        [TestCase(0f, 0f, 0f, 0.6f, 128)]
        [TestCase(0f, 0f, 0f, 0.42f, 30)]
        [TestCase(1f, 0f, 0f, 0.24f, 30)]
        [TestCase(1f, 0f, 0f, 0.24f, 180)]
        [TestCase(1f, 1f, 1f, 0.22f, 16)]
        [TestCase(10f / 255f, 14f / 255f, 27f / 255f, 0.8f, 243)]
        public void UIの面の上の半透明はWebの式と同じ色になる(float red, float green, float blue, float alpha, int ground)
        {
            var swatch = new VisualElement();
            swatch.style.width = 40;
            swatch.style.height = 40;
            swatch.style.backgroundColor = new Color(red, green, blue, alpha);
            var groundColor = new Color32((byte)ground, (byte)ground, (byte)ground, 255);
            var texture = UiRuntimePanelCapture.Render(swatch, 1280, 720, groundColor, UiCaptureBackdrop.GroundColorOnly, UiCaptureLocalization.InitializeDictionary);

            // 読み戻しは左下原点。左上 40×40 の中央を読む
            // Read-back is bottom-left origin; read the centre of the top-left 40x40
            var pixel = (Color32)texture.GetPixel(20, 720 - 20);
            Object.DestroyImmediate(texture);
            Assert.AreEqual(Web(red), pixel.r, 1, "R");
            Assert.AreEqual(Web(green), pixel.g, 1, "G");
            Assert.AreEqual(Web(blue), pixel.b, 1, "B");

            float Web(float channel) => Mathf.Round(channel * 255f * alpha + ground * (1f - alpha));
        }
    }
}
