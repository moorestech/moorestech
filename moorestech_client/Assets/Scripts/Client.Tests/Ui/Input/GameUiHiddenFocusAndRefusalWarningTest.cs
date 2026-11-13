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
    /// 表示されなくなったフォーカスを UITK 自身が外し根の入力規則が入力中の旗を下ろすことと、同じ要素の拒否の警告を 1 回にすることを実パネルで確かめる（L1）
    /// Verifies on a real panel that UITK itself blurs a focus that stops being displayed so the root input rules lower the text-input flag, and that refusals of the same element warn once (L1)
    /// </summary>
    /// <remarks>
    /// 隠れたフォーカスを外す部品は持たず UITK（Unity 6000.3 で実測）に任せている。Unity の版上げでこの挙動が変わったらここが赤になる
    /// No component of ours blurs a hidden focus; it relies on UITK (measured on Unity 6000.3), so this turns red if a Unity upgrade changes that behaviour
    /// </remarks>
    public class GameUiHiddenFocusAndRefusalWarningTest : UiTestHost
    {
        private static readonly Regex RefusedButtonWarning = new(@"^\[GameUiInput\] non text-field, unmarked element took focus and was blurred: Button 'refused-button'$");

        private VisualElement _rulesRoot;
        private VisualElement _screen;

        [SetUp]
        public void AttachRules()
        {
            // 規則はテストごとの器に付け、その下に隠したり出したりする画面を置く
            // Attach the rules to a per-test container and put a screen to hide and show under it
            _rulesRoot = new VisualElement { name = "rules-root", pickingMode = PickingMode.Ignore };
            _rulesRoot.style.position = Position.Absolute;
            _rulesRoot.style.left = 0;
            _rulesRoot.style.top = 0;
            _rulesRoot.style.right = 0;
            _rulesRoot.style.bottom = 0;
            rootVisualElement.Add(_rulesRoot);
            GameUiInputRules.Attach(_rulesRoot);
            _screen = new VisualElement { name = "hiding-screen" };
            _rulesRoot.Add(_screen);
        }

        [TearDown]
        public void ResetExclusivity()
        {
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.ClearEscapeLatchForTest();
        }

        [Test]
        public void 入力欄の画面を隠すと次の配置でフォーカスと入力中の旗が外れる()
        {
            var textField = AddToScreen(new TextField { name = "focus-text" });
            textField.Focus();
            Assert.IsTrue(UiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard), "前提: 入力中はキーボードのゲーム入力が止まる");

            _screen.style.display = DisplayStyle.None;
            simulate.FrameUpdate();

            Assert.IsNull(panel.focusController.focusedElement);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
            Assert.IsFalse(UiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));
        }

        [Test]
        public void 表示されたまま動いても入力欄のフォーカスは保つ()
        {
            var textField = AddToScreen(new TextField { name = "focus-text" });
            textField.Focus();

            _screen.style.marginLeft = 40;
            textField.style.width = 200;
            simulate.FrameUpdate();

            Assert.IsNotNull(panel.focusController.focusedElement);
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void 同じフレームで隠して戻すと入力欄のフォーカスは保つ()
        {
            var textField = AddToScreen(new TextField { name = "focus-text" });
            textField.Focus();
            simulate.FrameUpdate();

            // 配置の前に表示が戻っていれば表示されたままと同じに扱う
            // If display comes back before the layout, treat it as never hidden
            _screen.style.display = DisplayStyle.None;
            _screen.style.display = DisplayStyle.Flex;
            simulate.FrameUpdate();

            Assert.IsNotNull(panel.focusController.focusedElement);
            Assert.IsTrue(UiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));
        }

        [Test]
        public void 印付きのボタンも隠すとフォーカスが外れる()
        {
            var button = new Button { name = "keep-focus-button", focusable = true };
            UiKeyFocusKeepMark.Mark(button);
            AddToScreen(button);
            button.Focus();
            simulate.FrameUpdate();
            Assert.AreSame(button, panel.focusController.focusedElement, "前提: 印付きはフォーカスを保つ");

            _screen.style.display = DisplayStyle.None;
            simulate.FrameUpdate();

            Assert.IsNull(panel.focusController.focusedElement);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 同じ要素が続けてフォーカスを取っても警告は1回で入力欄を挟むとまた出す()
        {
            var button = AddToScreen(new Button { name = "refused-button", text = "refused-button" });
            var textField = AddToScreen(new TextField { name = "focus-text" });

            // 1 回目だけ警告し、2 回目は同じ異常の続きとして出さない
            // Warn on the first refusal only; the second is the same ongoing anomaly
            LogAssert.Expect(LogType.Warning, RefusedButtonWarning);
            FocusAndLetRulesBlur(button);
            FocusAndLetRulesBlur(button);
            LogAssert.NoUnexpectedReceived();

            // 入力欄が正しくフォーカスを取ると続きは途切れ、次の拒否でまた出す
            // A proper text-field focus ends the run, so the next refusal warns again
            textField.Focus();
            textField.Blur();
            LogAssert.Expect(LogType.Warning, RefusedButtonWarning);
            FocusAndLetRulesBlur(button);
            LogAssert.NoUnexpectedReceived();
        }

        private void FocusAndLetRulesBlur(Button button)
        {
            button.Focus();
            simulate.FrameUpdate();
            Assert.IsNull(panel.focusController.focusedElement);
        }

        private T AddToScreen<T>(T element) where T : VisualElement
        {
            element.style.width = 300;
            _screen.Add(element);
            simulate.FrameUpdate();
            return element;
        }
    }
}
