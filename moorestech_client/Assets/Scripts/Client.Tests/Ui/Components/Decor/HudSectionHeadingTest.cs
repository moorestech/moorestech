using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Decor;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Decor
{
    /// <summary>
    /// HudSectionHeading の文・並び・既定の寸法と、祖先でのつまみの上書きを確かめる（L1）
    /// Verifies HudSectionHeading text, order and default sizes, plus knob overrides on an ancestor (L1)
    /// </summary>
    public class HudSectionHeadingTest : UiTestHost
    {
        private const string KnobSheetPath = "Assets/Scripts/Client.Tests/Ui/Components/Decor/DecorKnobOverrides.uss";
        private const float ContainerWidth = 250f;
        private VisualElement _screen;
        private HudSectionHeading _heading;

        [SetUp]
        public void AddHeading()
        {
            Localize.Initialize();
            var knobSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(KnobSheetPath);
            Assert.IsNotNull(knobSheet, KnobSheetPath);
            rootVisualElement.styleSheets.Add(knobSheet);
            _screen = new VisualElement();
            _screen.style.width = ContainerWidth;
            _heading = new HudSectionHeading("challenge-hud-label", new UiText(LocalizationKeys.Ui.Challenge.CurrentTitle));
            _heading.Add(new Label("Second Step"));
            _screen.Add(_heading);
            rootVisualElement.Add(_screen);
            simulate.FrameUpdate();
        }

        [Test]
        public void ラベルはtestidの名前で渡したキーの文を出す()
        {
            var label = _heading.Q<Label>("challenge-hud-label");
            Assert.IsNotNull(label);
            Assert.AreEqual(new UiText(LocalizationKeys.Ui.Challenge.CurrentTitle).Resolve(), label.text);
        }

        [Test]
        public void 足した子は罫線の下の本文の列に入る()
        {
            var child = _heading.contentContainer[0];
            Assert.IsTrue(child.hierarchy.parent.ClassListContains("hud-section-heading__list"));
            Assert.Greater(child.worldBound.y, Rule().worldBound.yMax);
        }

        [Test]
        public void 既定は文字がtext_mutedでチャレンジHUDの寸法と罫線の幅いっぱい()
        {
            var label = HeadingLabel();
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--text-muted"), label.resolvedStyle.color, "color");
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--challenge-hud-label-font-size"), label.resolvedStyle.fontSize, 0.01f);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--challenge-hud-letter-spacing"), label.resolvedStyle.letterSpacing, 0.01f);
            Assert.AreEqual(ContainerWidth, Rule().worldBound.width, 0.5f);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--challenge-hud-label-rule-gap"), Rule().worldBound.y - label.worldBound.yMax, 1f);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--challenge-hud-rule-list-gap"), List().worldBound.y - Rule().worldBound.yMax, 1f);
        }

        [Test]
        public void 祖先でつまみを上書きすると罫線の幅と間隔と文字の大きさが変わる()
        {
            _screen.AddToClassList("test-knob-hud-heading");
            simulate.FrameUpdate();
            Assert.AreEqual(88f, Rule().worldBound.width, 0.5f);
            Assert.AreEqual(17f, HeadingLabel().resolvedStyle.fontSize, 0.01f);
            Assert.AreEqual(9f, Rule().worldBound.y - HeadingLabel().worldBound.yMax, 1f);
            Assert.AreEqual(3f, List().worldBound.y - Rule().worldBound.yMax, 1f);
        }

        [Test]
        public void 罫線は拾わず描画でエラーを出さない()
        {
            Assert.AreEqual(PickingMode.Ignore, _heading.Q(className: "hud-section-heading__rule").pickingMode);
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();
        }

        private Label HeadingLabel()
        {
            return _heading.Q<Label>(className: "hud-section-heading__label");
        }

        private VisualElement Rule()
        {
            return _heading.Q(className: "fade-rule");
        }

        private VisualElement List()
        {
            return _heading.Q(className: "hud-section-heading__list");
        }
    }
}
