using Client.Input;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Input;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Input
{
    /// <summary>
    /// 取消のナビゲーション（Input System へ注入した ESC・パッドの取消）でも入力欄は ESC と同じくフォーカスを外す（L1）
    /// Navigation cancel (ESC injected into the Input System, pad cancel) drops text-field focus the same way as ESC (L1)
    /// </summary>
    /// <remarks>
    /// EventSystem 経由の UITK（PanelEventHandler）は KeyDownEvent を OS の IMGUI イベントからだけ作る。録画の注入入力では ESC が NavigationCancelEvent としてだけ届く
    /// UITK through the EventSystem (PanelEventHandler) builds KeyDownEvent only from OS IMGUI events; in recordings the injected ESC arrives only as NavigationCancelEvent
    /// </remarks>
    public class GameUiInputRulesNavigationCancelTest : UiTestHost
    {
        private VisualElement _rulesRoot;

        [SetUp]
        public void AttachRules()
        {
            // テストごとの器に規則を付け、基底の Clear で一緒に捨てる（前例 GameUiInputRulesTest）
            // Attach the rules to a per-test container discarded by the base Clear (precedent GameUiInputRulesTest)
            _rulesRoot = new VisualElement { name = "rules-root", pickingMode = PickingMode.Ignore };
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
        public void 入力欄の取消ナビゲーションは値を保ってフォーカスを外しESCを消費する()
        {
            var textField = AddTextField();
            textField.Focus();
            simulate.TypingText("abc");
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);

            SendNavigationCancel(panel.focusController.focusedElement);

            Assert.AreEqual("abc", textField.value);
            Assert.IsNull(panel.focusController.focusedElement);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
            Assert.IsTrue(UiInputExclusivity.IsEscapeConsumedThisFrame);
        }

        [Test]
        public void 入力欄にフォーカスが無い取消ナビゲーションはESCを消費しない()
        {
            AddTextField();
            SendNavigationCancel(rootVisualElement.panel.visualTree);
            Assert.IsFalse(UiInputExclusivity.IsEscapeConsumedThisFrame);
        }

        [Test]
        public void ESCのキーで外れた後に同じフレームの取消ナビゲーションが来ても二重に動かない()
        {
            // 実キーボードでは KeyDown の後に同じ ESC の取消ナビゲーションが続く（PanelEventHandler.OnCancel が IMGUI のキーを先に流す）
            // With a real keyboard, the same ESC's navigation cancel follows the KeyDown (PanelEventHandler.OnCancel flushes IMGUI keys first)
            var textField = AddTextField();
            textField.Focus();
            var focusedBeforeKey = panel.focusController.focusedElement;
            simulate.KeyDown(UnityEngine.KeyCode.Escape);
            Assert.IsNull(panel.focusController.focusedElement);

            // OnCancel は IMGUI のキーを流す前に宛先を決めるので、外れた後も元の入力要素へ届く
            // OnCancel picks the target before flushing IMGUI keys, so it still reaches the former input element after the blur
            SendNavigationCancel(focusedBeforeKey);
            Assert.IsNull(panel.focusController.focusedElement);
            Assert.IsTrue(UiInputExclusivity.IsEscapeConsumedThisFrame);
        }

        private static void SendNavigationCancel(Focusable target)
        {
            // PanelEventHandler.OnCancel と同じく、フォーカス中の要素（無ければパネルの根）へ送る
            // Send to the focused element (or the panel root), as PanelEventHandler.OnCancel does
            using var evt = NavigationCancelEvent.GetPooled();
            evt.target = target;
            ((VisualElement)target).panel.visualTree.SendEvent(evt);
        }

        private TextField AddTextField()
        {
            var textField = new TextField { name = "rules-text" };
            textField.style.width = 300;
            _rulesRoot.Add(textField);
            simulate.FrameUpdate();
            return textField;
        }
    }
}
