// moorestech_client/Assets/Scripts/Client.Tests/Ui/Text/UiTypographyClassTest.cs
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Text.Typography
{
    /// <summary>
    /// Typography が Mantine を写す確認
    /// Checks Typography copies Mantine
    /// </summary>
    public class UiTypographyClassTest : UiTestHost
    {
        // 行の高さは USS に無いので min-height = 字の大きさ × Mantine の line-height。Title も Web と同じ px（A-37）
        // USS has no line-height, so min-height = font size x Mantine line-height; titles use the Web px too (A-37)
        [TestCase(UiTypographyClass.TextXs, 12f, 16.8f)]
        [TestCase(UiTypographyClass.TextSm, 14f, 20.3f)]
        [TestCase(UiTypographyClass.TextMd, 16f, 24.8f)]
        [TestCase(UiTypographyClass.TitleOrder1, 34f, 44.2f)]
        [TestCase(UiTypographyClass.TitleOrder2, 26f, 35.1f)]
        [TestCase(UiTypographyClass.TitleOrder3, 22f, 30.8f)]
        [TestCase(UiTypographyClass.TitleOrder4, 18f, 26.1f)]
        public void 大きさのクラスは字の大きさと行の高さを当て余白を消す(string className, float fontSize, float lineHeight)
        {
            var label = Mount(className);

            Assert.AreEqual(fontSize, label.resolvedStyle.fontSize, 0.01f);
            Assert.AreEqual(lineHeight, label.resolvedStyle.minHeight.value, 0.01f);
            // レイアウトは画素に丸められ 20.3 が 20.0 になるので、0.5px 以内で見る
            // Layout snaps to pixels (20.3 becomes 20.0), so check within 0.5px
            Assert.GreaterOrEqual(label.worldBound.height, lineHeight - 0.5f);
            Assert.AreEqual(0f, label.resolvedStyle.marginTop, 0.01f);
            Assert.AreEqual(0f, label.resolvedStyle.paddingLeft, 0.01f);
        }

        [TestCase(UiTypographyClass.TextDimmed, 0x82, 0x82, 0x82)]
        [TestCase(UiTypographyClass.TextWhite, 0xff, 0xff, 0xff)]
        [TestCase(UiTypographyClass.TextDefault, 0xee, 0xee, 0xee)]
        [TestCase(UiTypographyClass.TextMuted, 0xa6, 0xa7, 0xab)]
        [TestCase(UiTypographyClass.TextInsufficient, 0xff, 0x78, 0x78)]
        [TestCase(UiTypographyClass.TextHighContrast, 0xff, 0xff, 0xff)]
        public void 色のクラスはWebの文字色を当てる(string className, int r, int g, int b)
        {
            var label = Mount(className);

            var expected = new Color(r / 255f, g / 255f, b / 255f, 1f);
            Assert.AreEqual(expected.r, label.resolvedStyle.color.r, 0.005f);
            Assert.AreEqual(expected.g, label.resolvedStyle.color.g, 0.005f);
            Assert.AreEqual(expected.b, label.resolvedStyle.color.b, 0.005f);
        }

        [Test]
        public void 中央揃えのクラスは大きさのクラスより後に効く()
        {
            var label = Mount(UiTypographyClass.TextSm);
            label.AddToClassList(UiTypographyClass.TextCenter);
            simulate.FrameUpdate();

            Assert.AreEqual(TextAnchor.MiddleCenter, label.resolvedStyle.unityTextAlign);
        }

        [Test]
        public void キーヒントのクラスは子の文字へ大きさと字間と色を継がせる()
        {
            var container = new VisualElement();
            container.AddToClassList(UiTypographyClass.KeyHintText);
            var child = new Label("E");
            container.Add(child);
            rootVisualElement.Add(container);
            simulate.FrameUpdate();

            Assert.AreEqual(25f, child.resolvedStyle.fontSize, 0.01f);
            Assert.AreEqual(5.5f, child.resolvedStyle.letterSpacing, 0.01f);
            Assert.AreEqual(1f, child.resolvedStyle.color.r, 0.005f);
        }

        private Label Mount(string className)
        {
            var label = new Label("Typography");
            label.AddToClassList(className);
            rootVisualElement.Add(label);
            simulate.FrameUpdate();
            return label;
        }
    }
}
