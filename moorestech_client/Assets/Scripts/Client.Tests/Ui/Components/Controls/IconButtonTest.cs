using Client.Input;
using Client.Tests.Ui.TestDoubles;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Input.OsCursor;
using NUnit.Framework;
using static Client.Tests.Ui.Components.ComponentStyleAssert;
using UniRx;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Controls
{
    /// <summary>
    /// IconButton の押下・非フォーカス・寸法・ホバー色・つまみ上書きを確かめる（L1）
    /// Verifies IconButton presses, no focus, size, hover colour and knob overrides (L1)
    /// </summary>
    public class IconButtonTest : UiTestHost
    {
        private const string TestId = "icon-button-test";
        private const string KnobOverridePath = "Assets/Scripts/Client.Tests/Ui/Components/Controls/ControlKnobOverrides.uss";

        private VisualElement _rulesRoot;

        [SetUp]
        public void AttachRules()
        {
            // 基底はルートのコールバックをテスト間で消さないので、テストごとの器に規則を付ける（GameUiInputRulesTest と同じ形）
            // The base keeps root callbacks across tests, so attach the rules to a per-test container (same shape as GameUiInputRulesTest)
            _rulesRoot = new VisualElement { name = "rules-root" };
            _rulesRoot.style.position = Position.Absolute;
            _rulesRoot.style.left = 0;
            _rulesRoot.style.top = 0;
            _rulesRoot.style.right = 0;
            _rulesRoot.style.bottom = 0;
            _rulesRoot.style.alignItems = Align.FlexStart;
            rootVisualElement.Add(_rulesRoot);
            GameUiInputRules.Attach(_rulesRoot);
        }

        [TearDown]
        public void ResetExclusivity()
        {
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.ClearEscapeLatchForTest();
        }

        [Test]
        public void クリックで押下を1回流しキーと決定では押されずフォーカスを持たない()
        {
            var button = AddButton(_rulesRoot);
            var pressCount = 0;
            using var subscription = button.OnClicked.Subscribe(_ => pressCount++);

            simulate.Click(button.worldBound.center);
            simulate.KeyPress(KeyCode.Space);
            simulate.KeyPress(KeyCode.Return);

            Assert.AreEqual(1, pressCount);
            Assert.IsFalse(button.focusable);
            Assert.IsNull(panel.focusController.focusedElement);
            Assert.AreEqual(TestId, button.name);
        }

        [Test]
        public void 既定はWebのアイコンボタン寸法で面と枠を持たず図形はポインタを受けない()
        {
            // --icon-button-size = --slot-size / 2 = 24px、--icon-button-icon-size = --slot-size / 3 = 16px（Tokens.uss）
            // --icon-button-size = --slot-size / 2 = 24px and --icon-button-icon-size = --slot-size / 3 = 16px (Tokens.uss)
            var button = AddButton(_rulesRoot);
            var glyph = button.Q<IconButtonCloseGlyph>();

            Assert.AreEqual(24f, button.resolvedStyle.width, 0.5f);
            Assert.AreEqual(24f, button.resolvedStyle.height, 0.5f);
            Assert.AreEqual(16f, glyph.resolvedStyle.width, 0.5f);
            Assert.AreEqual(16f, glyph.resolvedStyle.height, 0.5f);
            Assert.AreEqual(0f, button.resolvedStyle.borderLeftWidth);
            Assert.AreEqual(0f, button.resolvedStyle.borderTopLeftRadius);
            Assert.AreEqual(0f, button.resolvedStyle.paddingLeft);
            Assert.AreEqual(0f, button.resolvedStyle.backgroundColor.a, 0.001f);
            Assert.AreEqual(PickingMode.Ignore, glyph.pickingMode);
            Assert.IsTrue(glyph.ClassListContains(IconButton.GlyphUssClassName));
        }

        [Test]
        public void 図形の色は既定の従属色で有効時のホバーだけ高コントラストになる()
        {
            var button = AddButton(_rulesRoot);
            var glyph = button.Q(className: IconButton.GlyphUssClassName);
            var resolvedCount = 0;
            glyph.RegisterCallback<CustomStyleResolvedEvent>(_ => resolvedCount++);
            AssertColor(Hex("#A6A7AB"), GlyphColor(glyph), "glyph");

            // ホバーで図形の色が上がる
            // Hover lifts the glyph colour
            simulate.MouseMove(button.worldBound.center);
            simulate.FrameUpdate();
            AssertColor(Hex("#FFFFFF"), GlyphColor(glyph), "glyph");
            Assert.Greater(resolvedCount, 0);

            // 無効ではホバーしても持ち上げない
            // Disabled never takes the hover lift
            button.SetEnabled(false);
            simulate.MouseMove(button.worldBound.center + Vector2.one);
            simulate.FrameUpdate();
            AssertColor(Hex("#A6A7AB"), GlyphColor(glyph), "glyph");
        }

        [Test]
        public void 画面がつまみを上書きするとボタンと図形の寸法が変わる()
        {
            // スキットのツールボタンと同じ上書き
            // Same override as the skit tool buttons
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(KnobOverridePath);
            Assert.IsNotNull(sheet, KnobOverridePath);
            var screenRoot = new VisualElement();
            screenRoot.styleSheets.Add(sheet);
            screenRoot.AddToClassList("control-knob-override");
            _rulesRoot.Add(screenRoot);
            var button = AddButton(screenRoot);

            Assert.AreEqual(33.3f, button.resolvedStyle.width, 0.5f);
            Assert.AreEqual(33.3f, button.Q<IconButtonCloseGlyph>().resolvedStyle.width, 0.5f);
        }

        private IconButton AddButton(VisualElement parent)
        {
            var button = new IconButton(TestId, new IconButtonCloseGlyph(), new UiOsCursorRequestStack(new FakeUiOsCursorApplier()));
            parent.Add(button);
            simulate.FrameUpdate();
            return button;
        }

        private static Color GlyphColor(VisualElement glyph)
        {
            Assert.IsTrue(glyph.customStyle.TryGetValue(new CustomStyleProperty<Color>(IconButton.GlyphColorProperty), out var color), IconButton.GlyphColorProperty);
            return color;
        }
    }
}
