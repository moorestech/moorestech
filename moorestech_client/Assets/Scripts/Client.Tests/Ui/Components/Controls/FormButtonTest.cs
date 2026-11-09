using Client.Input;
using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using static Client.Tests.Ui.Components.ComponentStyleAssert;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Controls
{
    /// <summary>
    /// FormButton の押下・無効・寸法と色・系統と大きさの切替を確かめる（L1）
    /// Verifies FormButton presses, disabled state, size, colours and variant/size switching (L1)
    /// </summary>
    public class FormButtonTest : UiTestHost
    {
        private const string TestId = "form-button-test";

        private VisualElement _rulesRoot;

        [SetUp]
        public void AttachRules()
        {
            // 基底はルートのコールバックをテスト間で消さないので、テストごとの器に規則を付ける。文言は載った時点で辞書を引くので先に読む
            // The base keeps root callbacks across tests, so attach the rules to a per-test container; labels look up the dictionary on attach, so load it first
            _rulesRoot = new VisualElement { name = "rules-root" };
            _rulesRoot.style.position = Position.Absolute;
            _rulesRoot.style.left = 0;
            _rulesRoot.style.top = 0;
            _rulesRoot.style.right = 0;
            _rulesRoot.style.bottom = 0;
            _rulesRoot.style.alignItems = Align.FlexStart;
            rootVisualElement.Add(_rulesRoot);
            GameUiInputRules.Attach(_rulesRoot);
            Localize.Initialize();
        }

        [TearDown]
        public void ResetExclusivity()
        {
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.ClearEscapeLatchForTest();
        }

        [Test]
        public void クリックで押下を1回流しキーと無効では押されずフォーカスを持たない()
        {
            var button = AddButton(FormButtonVariant.Filled);
            var pressCount = 0;
            using var subscription = button.OnClicked.Subscribe(_ => pressCount++);

            simulate.Click(button.worldBound.center);
            simulate.KeyPress(KeyCode.Space);
            simulate.KeyPress(KeyCode.Return);
            Assert.AreEqual(1, pressCount);
            Assert.IsNull(panel.focusController.focusedElement);

            button.SetEnabled(false);
            simulate.FrameUpdate();
            simulate.Click(button.worldBound.center);
            Assert.AreEqual(1, pressCount);
        }

        [Test]
        public void FilledはMantineのsmの寸法と青の面と白い文字でホバーで濃い青になる()
        {
            // --button-height-sm 36px・--button-padding-x-sm 18px・--mantine-radius-default 4px・枠 1px 透明・--mantine-font-size-sm 14px・blue-8 #1971c2・白
            // --button-height-sm 36px, --button-padding-x-sm 18px, --mantine-radius-default 4px, 1px transparent border, --mantine-font-size-sm 14px, blue-8 #1971c2, white
            var button = AddButton(FormButtonVariant.Filled);
            Assert.AreEqual(36f, button.resolvedStyle.height, 0.5f);
            Assert.AreEqual(18f, button.resolvedStyle.paddingLeft, 0.01f);
            Assert.AreEqual(18f, button.resolvedStyle.paddingRight, 0.01f);
            Assert.AreEqual(4f, button.resolvedStyle.borderTopLeftRadius, 0.01f);
            Assert.AreEqual(1f, button.resolvedStyle.borderTopWidth, 0.01f);
            Assert.AreEqual(0f, button.resolvedStyle.borderTopColor.a, 0.001f);
            Assert.AreEqual(14f, Label(button).resolvedStyle.fontSize, 0.01f);
            AssertColor(Hex("#1971C2"), button.resolvedStyle.backgroundColor, "face");
            AssertColor(Hex("#FFFFFF"), Label(button).resolvedStyle.color, "text");

            // 文言は 500 の字形（実測で Web の 600 に近い方）
            // The label uses the 500 face (measured nearest to the Web's 600)
            Assert.AreEqual("GameUiLatinMedium SDF", Label(button).resolvedStyle.unityFontDefinition.fontAsset.name);

            // ホバーは blue-9（--mantine-color-blue-filled-hover）
            // Hover is blue-9 (--mantine-color-blue-filled-hover)
            simulate.MouseMove(button.worldBound.center);
            simulate.FrameUpdate();
            AssertColor(Hex("#1864AB"), button.resolvedStyle.backgroundColor, "face");
        }

        [Test]
        public void 無効はMantineの無効色で不透明のまま()
        {
            // --mantine-color-disabled = dark-6 #2e2e2e、--mantine-color-disabled-color = dark-3 #696969。既定テーマの半透明は付けない
            // --mantine-color-disabled = dark-6 #2e2e2e and --mantine-color-disabled-color = dark-3 #696969; no default-theme translucency
            var button = AddButton(FormButtonVariant.Filled);
            button.SetEnabled(false);
            simulate.FrameUpdate();

            AssertColor(Hex("#2E2E2E"), button.resolvedStyle.backgroundColor, "face");
            AssertColor(Hex("#696969"), Label(button).resolvedStyle.color, "text");
            Assert.AreEqual(1f, button.resolvedStyle.opacity, 0.001f);
            Assert.AreEqual(1f, Label(button).resolvedStyle.opacity, 0.001f);
        }

        [Test]
        public void 無効のままホバーしても無効の面が勝つ()
        {
            // :disabled を :hover より後に置く順序の退行を捕まえる
            // Catches a regression of the rule order that keeps :disabled after :hover
            var button = AddButton(FormButtonVariant.Filled);
            button.SetEnabled(false);
            simulate.MouseMove(button.worldBound.center);
            simulate.FrameUpdate();
            AssertColor(Hex("#2E2E2E"), button.resolvedStyle.backgroundColor, "face");
        }

        [Test]
        public void 色の系統は1つだけ付き切り替えると入れ替わる()
        {
            // Danger は color="red" の filled（red-8 #e03131）
            // Danger is the filled red (red-8 #e03131)
            var button = AddButton(FormButtonVariant.Danger);
            AssertColor(Hex("#E03131"), button.resolvedStyle.backgroundColor, "face");

            // Subtle は面が透明・文字が blue-light-color（blue-3 #74c0fc）・ホバーで blue-light-hover（rgba(34, 139, 230, 0.2)）
            // Subtle has a clear face, blue-light-color text (blue-3 #74c0fc) and blue-light-hover on hover (rgba(34, 139, 230, 0.2))
            button.SetVariant(FormButtonVariant.Subtle);
            simulate.FrameUpdate();
            Assert.IsFalse(button.ClassListContains(FormButton.DangerUssClassName));
            Assert.IsFalse(button.ClassListContains(FormButton.FilledUssClassName));
            Assert.IsTrue(button.ClassListContains(FormButton.SubtleUssClassName));
            Assert.AreEqual(0f, button.resolvedStyle.backgroundColor.a, 0.001f);
            AssertColor(Hex("#74C0FC"), Label(button).resolvedStyle.color, "text");
            simulate.MouseMove(button.worldBound.center);
            simulate.FrameUpdate();
            var hoverFace = button.resolvedStyle.backgroundColor;
            Assert.AreEqual(34f / 255f, hoverFace.r, 0.01f);
            Assert.AreEqual(139f / 255f, hoverFace.g, 0.01f);
            Assert.AreEqual(230f / 255f, hoverFace.b, 0.01f);
            Assert.Greater(hoverFace.a, 0f);
        }

        [Test]
        public void ExtraLargeはMantineのxlの寸法になる()
        {
            // --button-height-xl 60px・--button-padding-x-xl 32px・--mantine-font-size-xl 20px（出展モードの言語ゲートの size="xl"）
            // --button-height-xl 60px, --button-padding-x-xl 32px, --mantine-font-size-xl 20px (the event language gate's size="xl")
            var button = AddButton(FormButtonVariant.Filled);
            button.SetSize(FormButtonSize.ExtraLarge);
            simulate.FrameUpdate();

            Assert.AreEqual(60f, button.resolvedStyle.height, 0.5f);
            Assert.AreEqual(32f, button.resolvedStyle.paddingLeft, 0.01f);
            Assert.AreEqual(20f, Label(button).resolvedStyle.fontSize, 0.01f);
        }

        [Test]
        public void 名前はtestidで文言は差し替えられる()
        {
            var button = AddButton(FormButtonVariant.Filled);
            Assert.AreEqual(TestId, button.name);
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.PauseMenu.Settings), Label(button).text);

            button.SetLabel(new UiText(LocalizationKeys.Ui.PauseMenu.BugReport));
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.PauseMenu.BugReport), Label(button).text);

            // モーダル要求のボタン文言のような解決済みの文もそのまま出す（設計書 A-40）
            // Already-resolved text such as a modal request's button label shows as is (design A-40)
            button.SetLabel(UiText.FromVerbatim("Save"));
            Assert.AreEqual("Save", Label(button).text);
        }

        private FormButton AddButton(FormButtonVariant variant)
        {
            var button = new FormButton(TestId, new UiText(LocalizationKeys.Ui.PauseMenu.Settings), variant);
            _rulesRoot.Add(button);
            simulate.FrameUpdate();
            return button;
        }

        private static Label Label(FormButton button)
        {
            return button.Q<Label>(className: FormButton.LabelUssClassName);
        }
    }
}
