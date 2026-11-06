using Client.Input;
using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Controls
{
    /// <summary>
    /// PanelActionButton のクリック・キーで押されないこと・フォーカスを持たないこと・Web の寸法を入力規則付きのパネルで確かめる（L1）
    /// Verifies PanelActionButton clicks, no key presses, no focus and Web dimensions on a panel with the input rules (L1)
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

            _button = new PanelActionButton(TestId, LocalizationKeys.Ui.PauseMenu.Settings);
            _clickCount = 0;
            _button.clicked += () => _clickCount++;
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
            // クリックした後でもフォーカスを持たないので、キーと決定は届かない
            // Even after a click it holds no focus, so keys and submit never reach it
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
    }
}
