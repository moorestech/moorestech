using Client.Tests.Ui.TestHost;
using Client.Tests.Ui.Components;
using Client.Ui.Foundation.Text;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text
{
    /// <summary>
    /// 字間トークンが Web と同じ字幅を足すことを、字間 0 との幅の差で確かめる
    /// Verifies letter-spacing tokens add the Web's width per gap, measured against zero spacing
    /// </summary>
    public class UiLetterSpacingWebWidthTest : UiTestHost
    {
        // 字の形に依らず字間の数だけを数えるため、同じ字を並べる
        // Repeat one glyph so only the number of gaps matters
        private const string SampleText = "IIIIIIIIII";
        private const int GapCount = 9;

        [Test]
        public void キーヒントの字間はWebの0_055emと同じ幅を足す()
        {
            var spaced = MountKeyHint();
            var unspaced = MountKeyHint();
            unspaced.style.letterSpacing = 0f;
            simulate.FrameUpdate();

            // Web は 0.055em × 25px = 1.375px を字ごとに足す
            // The Web adds 0.055em x 25px = 1.375px per glyph
            var webGapPixels = 0.055f * spaced.resolvedStyle.fontSize;
            Assert.AreEqual(webGapPixels * GapCount, spaced.layout.width - unspaced.layout.width, 1f, $"{spaced.layout.width} vs {unspaced.layout.width}");
        }

        [TestCase("--challenge-hud-letter-spacing", "--challenge-hud-label-font-size")]
        [TestCase("--operation-hud-letter-spacing", "--operation-hud-label-font-size")]
        public void HUD見出しの字間はWebの1pxと同じ幅を足す(string letterSpacingToken, string fontSizeToken)
        {
            var fontSize = ThemeTokenFile.ResolvePixels(fontSizeToken);
            var spaced = MountLabel(fontSize, ThemeTokenFile.ResolvePixels(letterSpacingToken));
            var unspaced = MountLabel(fontSize, 0f);
            simulate.FrameUpdate();

            const float webGapPixels = 1f;
            Assert.AreEqual(webGapPixels * GapCount, spaced.layout.width - unspaced.layout.width, 1f, $"{spaced.layout.width} vs {unspaced.layout.width}");
        }

        private Label MountKeyHint()
        {
            var container = new VisualElement();
            container.AddToClassList(UiTypographyClass.KeyHintText);
            container.style.alignItems = Align.FlexStart;
            var label = new Label(SampleText);
            container.Add(label);
            rootVisualElement.Add(container);
            return label;
        }

        private Label MountLabel(float fontSize, float letterSpacing)
        {
            var label = new Label(SampleText);
            label.style.alignSelf = Align.FlexStart;
            label.style.fontSize = fontSize;
            label.style.letterSpacing = letterSpacing;
            rootVisualElement.Add(label);
            return label;
        }
    }
}
