using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Decor;
using Client.Ui.Foundation.Theme;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Decor
{
    /// <summary>
    /// FadeRule の寸法・色の出所・拾わないことを本番テーマのパネルで確かめる（L1）
    /// Verifies FadeRule size, color source and picking on a panel with the production theme (L1)
    /// </summary>
    public class FadeRuleTest : UiTestHost
    {
        private const float ContainerWidth = 240f;
        private FadeRule _rule;

        [SetUp]
        public void AddRule()
        {
            var container = new VisualElement();
            container.style.width = ContainerWidth;
            _rule = new FadeRule();
            container.Add(_rule);
            rootVisualElement.Add(container);
            simulate.FrameUpdate();
        }

        [Test]
        public void 高さ2pxで親の幅いっぱいに伸びる()
        {
            Assert.AreEqual(2f, _rule.layout.height, 0.5f);
            Assert.AreEqual(ContainerWidth, _rule.layout.width, 0.5f);
        }

        [Test]
        public void 色はGamePanelの下の罫線と同じ前計算トークンを読む()
        {
            // Web の FadeRule は GamePanel の下の罫線と同じ rgb(128 132 159) 30/42/26%
            // The Web FadeRule shares rgb(128 132 159) 30/42/26% with the GamePanel bottom rule
            AssertReads("--fade-rule-start", "--game-panel-rule-bottom-start");
            AssertReads("--fade-rule-peak", "--game-panel-rule-bottom-peak");
            AssertReads("--fade-rule-end", "--game-panel-rule-bottom-end");
        }

        [Test]
        public void 拾わない()
        {
            Assert.AreEqual(PickingMode.Ignore, _rule.pickingMode);
        }

        [Test]
        public void 描画でエラーを出さない()
        {
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();
        }

        private void AssertReads(string knob, string token)
        {
            Assert.IsTrue(UssCustomStyleReader.TryReadColor(_rule.customStyle, knob, out var actual), knob);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor(token), actual, knob);
        }
    }
}
