using Client.Input;
using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ComponentStyleAssert;

namespace Client.Tests.Ui.Components.Controls
{
    /// <summary>
    /// PanelActionButtonを実パネルで確かめる（L1）
    /// Verifies PanelActionButton on a real panel (L1)
    /// </summary>
    public class PanelActionButtonTest : UiTestHost
    {
        private const string TestId = "panel-action-button-test";

        private PanelActionButton _button;
        private int _clickCount;

        [SetUp]
        public void AddButtonUnderRules()
        {
            // 基底はルートのコールバックをテスト間で消さないので、テストごとの器に規則を付ける（GameUiInputRulesTest と同じ形）
            // The base keeps root callbacks across tests, so attach the rules to a per-test container (same shape as GameUiInputRulesTest)
            var rulesRoot = new VisualElement { name = "rules-root" };
            rulesRoot.style.position = Position.Absolute;
            rulesRoot.style.left = 0;
            rulesRoot.style.top = 0;
            rulesRoot.style.right = 0;
            rulesRoot.style.bottom = 0;
            rootVisualElement.Add(rulesRoot);
            GameUiInputRules.Attach(rulesRoot);

            // LocalizedLabel が載った時点で辞書を引くので先に読み込む
            // LocalizedLabel looks up the dictionary on attach, so load it first
            Localize.Initialize();

            _button = new PanelActionButton(TestId, new UiText(LocalizationKeys.Ui.PauseMenu.Settings));
            _clickCount = 0;
            _button.OnClicked.Subscribe(_ => _clickCount++);
            rulesRoot.Add(_button);
            simulate.FrameUpdate();
        }

        [TearDown]
        public void ResetExclusivity()
        {
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.ClearEscapeLatchForTest();
        }

        [Test]
        public void ClickPressesOnce()
        {
            simulate.Click(_button.worldBound.center);
            Assert.AreEqual(1, _clickCount);
        }

        [Test]
        public void SpaceEnterAndSubmitDoNotPress()
        {
            // クリック後もフォーカスなし
            // No focus even after a click
            simulate.Click(_button.worldBound.center);
            simulate.KeyPress(KeyCode.Space);
            simulate.KeyPress(KeyCode.Return);
            simulate.KeyPress(KeyCode.KeypadEnter);
            using (var evt = NavigationSubmitEvent.GetPooled())
            {
                rootVisualElement.panel.visualTree.SendEvent(evt);
            }
            Assert.AreEqual(1, _clickCount);
        }

        [Test]
        public void NeverTakesFocus()
        {
            Assert.IsFalse(_button.focusable);
            simulate.Click(_button.worldBound.center);
            _button.Focus();
            simulate.FrameUpdate();
            Assert.IsNull(panel.focusController.focusedElement);
        }

        [Test]
        public void NameIsTestidAndLabelIsLocalized()
        {
            Assert.AreEqual(TestId, _button.name);
            var label = _button.Q<LocalizedLabel>(className: "panel-action-button__label");
            Assert.IsNotNull(label);
            Assert.IsFalse(string.IsNullOrEmpty(label.text));
        }

        [Test]
        public void WebDimensionsBeatDefaultTheme()
        {
            // Web の --panel-action-button-width / height と直角・余白なし。レイアウトは画素へ丸めるので 0.5px まで許す
            // Web --panel-action-button-width / height, square corners, no margin; layout snaps to pixels, so allow 0.5px
            Assert.AreEqual(87f, _button.resolvedStyle.width, 0.5f);
            Assert.AreEqual(28.609f, _button.resolvedStyle.height, 0.5f);
            Assert.AreEqual(0f, _button.resolvedStyle.borderTopLeftRadius);
            Assert.AreEqual(0f, _button.resolvedStyle.marginLeft);
            Assert.AreEqual(0f, _button.resolvedStyle.borderLeftWidth);
        }

        [Test]
        public void DisabledKeepsWebFaceAndTextWithoutThemeDimming()
        {
            // Web の .button に :disabled の様式は無い（宇宙エレベーターの「接続する」の実測: 文字 rgb(255,255,255)・opacity 1・面は有効時と同じ）
            // The Web .button has no :disabled style (measured on the space elevator connect: text rgb(255,255,255), opacity 1, same face as enabled)
            var enabledFace = _button.resolvedStyle.backgroundColor;
            _button.SetEnabled(false);
            simulate.FrameUpdate();

            var label = _button.Q<LocalizedLabel>(className: "panel-action-button__label");
            AssertColor(Hex("#FFFFFF"), label.resolvedStyle.color, "text");
            AssertColor(enabledFace, _button.resolvedStyle.backgroundColor, "face");
            Assert.AreEqual(1f, _button.resolvedStyle.opacity, 0.001f);
            Assert.AreEqual(1f, label.resolvedStyle.opacity, 0.001f);
            Assert.AreEqual(0f, _button.resolvedStyle.translate.y, 0.001f);
        }
    }
}
