using System.Text.RegularExpressions;
using Client.Input;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Crosscut.Input
{
    /// <summary>
    /// ルートの入力規則の下で Space がチェックの切り替えにならず、入力欄には空白として入ることを確かめる（L1。Web e2e inputLayer.spec の写し）
    /// Verifies that under the root input rules Space never flips a toggle yet types a blank into a text field (L1; port of the Web e2e inputLayer.spec)
    /// </summary>
    public class GameUiSpaceKeyRulesTest : UiTestHost
    {
        private static readonly Regex RefusedFocusWarning = new(@"^\[GameUiInput\] non text-field, unmarked element took focus and was blurred: ");

        private VisualElement _rulesRoot;

        [SetUp]
        public void AttachRules()
        {
            // 規則はテストごとの器に付け、基底の Clear で一緒に捨てる（前例 GameUiInputRulesTest）
            // Attach the rules to a per-test container that the base Clear discards (precedent GameUiInputRulesTest)
            _rulesRoot = new VisualElement { name = "rules-root", pickingMode = PickingMode.Ignore };
            _rulesRoot.StretchToParentSize();
            rootVisualElement.Add(_rulesRoot);
            GameUiInputRules.Attach(_rulesRoot);
        }

        [TearDown]
        public void ResetExclusivity()
        {
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.ClearEscapeLatchForTest();
        }

        /// <web-test file="e2e/tests/regression/inputLayer.spec.ts">checkboxではSpaceでトグルせず、contenteditableには空白が入る</web-test>
        [Test]
        public void SpaceDoesNotFlipToggleButTypesBlankIntoTextField()
        {
            var toggle = new Toggle { name = "rules-toggle" };
            _rulesRoot.Add(toggle);
            simulate.FrameUpdate();

            // フォーカスを外すのは次の機会なので、外れる前に Space のキーと決定を送る
            // The blur lands at the next opportunity, so send the Space key and submit before it does
            LogAssert.Expect(LogType.Warning, RefusedFocusWarning);
            toggle.Focus();
            simulate.KeyDown(KeyCode.Space);
            simulate.KeyUp(KeyCode.Space);
            SendNavigationSubmit();
            simulate.FrameUpdate();
            Assert.IsFalse(toggle.value, "Space か決定でチェックが切り替わった");

            // 入力欄は文字入力なので Space の空白を受け取る
            // A text field takes text input, so it receives the blank from Space
            var textField = new TextField { name = "rules-text" };
            textField.style.width = 300;
            _rulesRoot.Add(textField);
            simulate.FrameUpdate();
            textField.Focus();
            simulate.TypingText("a b");
            Assert.AreEqual("a b", textField.value);
        }

        [Test]
        public void WithoutRulesSubmitFlipsToggle()
        {
            // 規則の外の Toggle は同じ決定で切り替わる（上の打ち消しが空振りでない証拠）
            // A toggle outside the rules flips on the same submit, proving the cancelling test is not vacuous
            var toggle = new Toggle { name = "free-toggle" };
            rootVisualElement.Add(toggle);
            simulate.FrameUpdate();
            toggle.Focus();
            SendNavigationSubmit();
            Assert.IsTrue(toggle.value);
        }

        private void SendNavigationSubmit()
        {
            using var evt = NavigationSubmitEvent.GetPooled();
            rootVisualElement.panel.visualTree.SendEvent(evt);
        }
    }
}
