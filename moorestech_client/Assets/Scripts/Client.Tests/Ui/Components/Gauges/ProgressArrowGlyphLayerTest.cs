using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Theme;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Gauges
{
    /// <summary>
    /// ProgressArrowGlyph の各層が USS から受け取る色・線幅・影の値と、層の箱・非表示から戻したときの当てはめ直しを確かめる
    /// Checks the colors, stroke width and shadow values each layer receives from USS, the layer boxes, and refitting after being hidden
    /// </summary>
    public class ProgressArrowGlyphLayerTest : UiTestHost
    {
        private const string ProbePath = "Assets/Scripts/Client.Tests/Ui/Components/Gauges/GaugeKnobOverrides.uss";

        [Test]
        public void 溝と充填と輪郭と影は各層のクラスでWebの色を受け取る()
        {
            var glyph = Mount(rootVisualElement);

            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--gauge-track"), LayerColor(glyph, "progress-arrow-glyph__track", "--progress-arrow-glyph-layer-face"), "track");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--color-content-primary"), LayerColor(glyph, "progress-arrow-glyph__fill", "--progress-arrow-glyph-layer-face"), "fill");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--craft-arrow-outline"), LayerColor(glyph, "progress-arrow-glyph__outline", "--progress-arrow-glyph-layer-stroke"), "outline");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--progress-arrow-glyph-shadow"), LayerColor(glyph, "progress-arrow-glyph__shadow", "--progress-arrow-glyph-layer-shadow"), "shadow");
        }

        [Test]
        public void 輪郭の線幅と影のずれとぼかしはWebのCSSの直書きの値()
        {
            var glyph = Mount(rootVisualElement);

            Assert.AreEqual(2f, LayerPixels(glyph, "progress-arrow-glyph__outline", "--progress-arrow-glyph-layer-stroke-width"), 0.001f);
            Assert.AreEqual(2f, LayerPixels(glyph, "progress-arrow-glyph__shadow", "--progress-arrow-glyph-layer-stroke-width"), 0.001f);
            Assert.AreEqual(1f, LayerPixels(glyph, "progress-arrow-glyph__shadow", "--progress-arrow-glyph-layer-shadow-offset-y"), 0.001f);
            Assert.AreEqual(1f, LayerPixels(glyph, "progress-arrow-glyph__shadow", "--progress-arrow-glyph-layer-shadow-blur"), 0.001f);
        }

        [Test]
        public void 祖先でゲージの色を上書きすると溝は従い充填の白は従わない()
        {
            // Web の充填は --gauge-fill でなく本文の白（§8.13 のユーザー裁定）
            // The Web fill is the content white, not --gauge-fill (the §8.13 user ruling)
            var probe = AssetDatabase.LoadAssetAtPath<StyleSheet>(ProbePath);
            Assert.IsNotNull(probe, ProbePath);
            rootVisualElement.styleSheets.Add(probe);
            var screen = new VisualElement();
            screen.AddToClassList("gauge-knob-probe");
            rootVisualElement.Add(screen);
            var glyph = Mount(screen);

            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--color-content-primary"), LayerColor(glyph, "progress-arrow-glyph__track", "--progress-arrow-glyph-layer-face"), "track");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--color-content-primary"), LayerColor(glyph, "progress-arrow-glyph__fill", "--progress-arrow-glyph-layer-face"), "fill");
        }

        [Test]
        public void 影と溝と輪郭はviewBoxの箱に重なる()
        {
            var glyph = Mount(rootVisualElement);

            var viewBox = glyph.Q(className: "progress-arrow-glyph__view-box").layout;
            foreach (var layerClass in new[] { "progress-arrow-glyph__shadow", "progress-arrow-glyph__track", "progress-arrow-glyph__outline" })
            {
                var layer = glyph.Q(className: layerClass).layout;
                // 層は viewBox の 4 辺に付くので丸めても同じ辺になる。丸めの 1 段だけ許す
                // Layers pin to the viewBox edges, so rounding lands on the same edges; allow one rounding step
                Assert.AreEqual(0f, layer.x, 1f, layerClass);
                Assert.AreEqual(0f, layer.y, 1f, layerClass);
                Assert.AreEqual(viewBox.width, layer.width, 1f, layerClass);
                Assert.AreEqual(viewBox.height, layer.height, 1f, layerClass);
            }
        }

        [Test]
        public void 祖先を非表示にして戻してもエラーを出さず元の箱へ当てはめ直す()
        {
            // 非表示の祖先の下では寸法が 0 になりうる。黙って飛ばし、戻したら当てはめ直す（D-25）
            // Under a hidden ancestor the size can be 0; skip silently and refit once shown again (D-25)
            var screen = new VisualElement();
            rootVisualElement.Add(screen);
            var glyph = Mount(screen);
            var before = glyph.Q(className: "progress-arrow-glyph__view-box").layout;

            screen.style.display = DisplayStyle.None;
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            screen.style.display = DisplayStyle.Flex;
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            LogAssert.NoUnexpectedReceived();
            var after = glyph.Q(className: "progress-arrow-glyph__view-box").layout;
            Assert.AreEqual(before.x, after.x, 0.05f);
            Assert.AreEqual(before.width, after.width, 0.05f);
            Assert.AreEqual(before.height, after.height, 0.05f);
        }

        private static Color LayerColor(ProgressArrowGlyph glyph, string layerClass, string property)
        {
            Assert.IsTrue(UssCustomStyleReader.TryReadColor(glyph.Q(className: layerClass).customStyle, property, out var color), $"{layerClass} {property}");
            return color;
        }

        private static float LayerPixels(ProgressArrowGlyph glyph, string layerClass, string property)
        {
            Assert.IsTrue(UssCustomStyleReader.TryReadPixels(glyph.Q(className: layerClass).customStyle, property, out var pixels), $"{layerClass} {property}");
            return pixels;
        }

        private ProgressArrowGlyph Mount(VisualElement parent)
        {
            var glyph = new ProgressArrowGlyph("arrow");
            glyph.SetProgress(0.5f);
            parent.Add(glyph);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            return glyph;
        }
    }
}
