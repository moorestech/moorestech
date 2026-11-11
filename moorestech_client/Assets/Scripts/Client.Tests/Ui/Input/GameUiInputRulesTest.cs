using System.Text.RegularExpressions;
using Client.Input;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Input
{
    /// <summary>
    /// ルートの入力規則（フォーカス同期・ESC・ナビゲーション打ち消し・入力欄以外のフォーカス拒否）を実パネルで確かめる（L1）
    /// Verifies the root input rules (focus sync, ESC, navigation cancelling, refusing non-text-field focus) on a real panel (L1)
    /// </summary>
    public class GameUiInputRulesTest : UiTestHost
    {
        private static readonly Regex ButtonFocusWarning = new(@"^\[GameUiInput\] non text-field, unmarked element took focus and was blurred: Button 'rules-button'$");

        private VisualElement _rulesRoot;

        [SetUp]
        public void AttachRules()
        {
            // 基底はルートのコールバックをテスト間で消さないので、テストごとの器に付けて基底の Clear で一緒に捨てる
            // The base keeps root callbacks across tests, so attach to a per-test container that the base Clear discards
            _rulesRoot = new VisualElement { name = "rules-root", pickingMode = PickingMode.Ignore };
            _rulesRoot.style.position = Position.Absolute;
            _rulesRoot.style.left = 0;
            _rulesRoot.style.top = 0;
            _rulesRoot.style.right = 0;
            _rulesRoot.style.bottom = 0;
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
        public void TextFieldFocusRaisesFlagAndLeavingClearsIt()
        {
            var textField = AddTextField(_rulesRoot, "rules-text", false);

            textField.Focus();
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
            textField.Blur();
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);

            // 空き領域のクリックでもフォーカスが外れて false に戻る
            // Clicking an empty spot also drops the focus and clears the flag
            textField.Focus();
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
            simulate.Click(new Vector2(1200, 700));
            Assert.IsNull(panel.focusController.focusedElement);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EscapeAfterCommittedTextKeepsValueAndDropsFocus(bool multiline)
        {
            var textField = AddTextField(_rulesRoot, "rules-text", multiline);
            textField.Focus();
            simulate.TypingText("abc");
            Assert.AreEqual("abc", textField.value);

            simulate.KeyDown(KeyCode.Escape);

            // 値は巻き戻らず、フォーカスだけ外れ、ESC はワールドへ渡らない
            // The value is not reverted, only the focus drops, and ESC does not reach the world
            Assert.AreEqual("abc", textField.value);
            Assert.IsNull(panel.focusController.focusedElement);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
            Assert.IsTrue(UiInputExclusivity.IsEscapeConsumedThisFrame);

            // 数フレーム進めても再フォーカスされない
            // No refocus happens after several frames
            for (var i = 0; i < 3; i++) simulate.FrameUpdate();
            Assert.AreEqual("abc", textField.value);
            Assert.IsNull(panel.focusController.focusedElement);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void EscapeWithoutTextFieldFocusIsNotConsumed()
        {
            AddTextField(_rulesRoot, "rules-text", false);

            simulate.KeyDown(KeyCode.Escape);
            Assert.IsFalse(UiInputExclusivity.IsEscapeConsumedThisFrame);
        }

        [Test]
        public void ButtonFocusIsBlurredWithWarning()
        {
            var button = AddButton(_rulesRoot, "rules-button");

            LogAssert.Expect(LogType.Warning, ButtonFocusWarning);
            button.Focus();
            simulate.FrameUpdate();

            Assert.IsNull(panel.focusController.focusedElement);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void KeepMarkedButtonFocusIsKeptWithoutWarning()
        {
            var button = new Button { name = "rules-keep-button", focusable = true };
            UiKeyFocusKeepMark.Mark(button);
            _rulesRoot.Add(button);
            button.Focus();
            for (var i = 0; i < 3; i++) simulate.FrameUpdate();
            // 印付きは規則の次のスケジュールでも外れず、警告も出ない。入力欄ではないので入力中の旗は立たない
            // A marked element survives the rules' scheduled blur without a warning, and raises no text-input flag
            Assert.AreSame(button, panel.focusController.focusedElement);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void NavigationSubmitDoesNotClickFocusedButton()
        {
            var button = AddButton(_rulesRoot, "rules-button");
            var clickCount = 0;
            button.clicked += () => clickCount++;

            // フォーカスを外すのは次の機会なので、外れる前に決定を送る
            // The blur happens at the next opportunity, so send submit before it lands
            LogAssert.Expect(LogType.Warning, ButtonFocusWarning);
            button.Focus();
            SendNavigationSubmit();
            Assert.AreEqual(0, clickCount);
        }

        [Test]
        public void NavigationMoveNextDoesNotMoveFocus()
        {
            var first = AddTextField(_rulesRoot, "rules-text-first", false);
            AddTextField(_rulesRoot, "rules-text-second", false);

            first.Focus();
            var focusedBefore = panel.focusController.focusedElement;
            SendNavigationMoveNext();
            Assert.AreSame(focusedBefore, panel.focusController.focusedElement);
        }

        [Test]
        public void WithoutRulesSubmitClicksButtonAndNextMovesFocus()
        {
            // 規則の外に置いた要素では同じ送り方で決定と移動が効く（上の打ち消しテストが空振りでない証拠）
            // Outside the rules the same events do click and move focus, proving the cancelling tests are not vacuous
            var button = AddButton(rootVisualElement, "free-button-first");
            AddButton(rootVisualElement, "free-button-second");
            var clickCount = 0;
            button.clicked += () => clickCount++;
            button.Focus();
            SendNavigationSubmit();
            Assert.AreEqual(1, clickCount);

            SendNavigationMoveNext();
            Assert.AreNotSame(button, panel.focusController.focusedElement);
        }

        private void SendNavigationSubmit()
        {
            using var evt = NavigationSubmitEvent.GetPooled();
            rootVisualElement.panel.visualTree.SendEvent(evt);
        }

        private void SendNavigationMoveNext()
        {
            using var evt = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next);
            rootVisualElement.panel.visualTree.SendEvent(evt);
        }

        private TextField AddTextField(VisualElement parent, string name, bool multiline)
        {
            var textField = new TextField { name = name, multiline = multiline };
            textField.style.width = 300;
            parent.Add(textField);
            simulate.FrameUpdate();
            return textField;
        }

        private Button AddButton(VisualElement parent, string name)
        {
            var button = new Button { name = name, text = name };
            parent.Add(button);
            simulate.FrameUpdate();
            return button;
        }
    }
}
