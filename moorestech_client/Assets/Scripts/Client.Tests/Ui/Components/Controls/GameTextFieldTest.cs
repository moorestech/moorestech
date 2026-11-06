using System.Collections.Generic;
using Client.Input;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Controls
{
    /// <summary>
    /// 共通入力欄を実パネルで確かめる（L1）
    /// Verifies the shared text field on a real panel (L1)
    /// </summary>
    public class GameTextFieldTest : UiTestHost
    {
        private const string TestId = "game-text-field-test";

        private VisualElement _rulesRoot;

        [SetUp]
        public void AttachRules()
        {
            // 基底はルートのコールバックをテスト間で消さないので、テストごとの器に規則を付ける（GameUiInputRulesTest と同じ形）
            // The base keeps root callbacks across tests, so attach the rules to a per-test container (same shape as GameUiInputRulesTest)
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

        [TestCase(false)]
        [TestCase(true)]
        public void ClickFocusDoesNotSelectAll(bool multiline)
        {
            var gameTextField = AddGameTextField(multiline);
            gameTextField.SetValueWithoutNotify("hello world");
            simulate.FrameUpdate();

            simulate.Click(gameTextField.Field.worldBound.center);
            simulate.FrameUpdate();

            // 選択範囲は空
            // The selection is empty
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
            Assert.AreEqual(gameTextField.Field.textSelection.cursorIndex, gameTextField.Field.textSelection.selectIndex);
        }

        [Test]
        public void OnValueChangedStreamsTypedValues()
        {
            var gameTextField = AddGameTextField(false);
            var received = new List<string>();
            using var subscription = gameTextField.OnValueChanged.Subscribe(received.Add);

            gameTextField.Field.Focus();
            simulate.TypingText("ab");

            Assert.AreEqual(new[] { "a", "ab" }, received);
            Assert.AreEqual("ab", gameTextField.Field.value);

            // 通知無しの設定は流れない
            // Setting without notify does not stream
            gameTextField.SetValueWithoutNotify("zz");
            Assert.AreEqual(2, received.Count);
            Assert.AreEqual("zz", gameTextField.Field.value);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CompositionUnderlineShowsOnlyWhileComposing(bool multiline)
        {
            var gameTextField = AddGameTextField(multiline);
            var underline = gameTextField.Q(className: ImeCompositionUnderline.UssClassName);
            gameTextField.Field.Focus();
            simulate.TypingText("abc");
            simulate.FrameUpdate();

            // 変換中でないときは出ない
            // Hidden while not composing
            Assert.AreEqual(DisplayStyle.None, underline.resolvedStyle.display);

            // 変換中の下線の幅・色・位置
            // Width, color and position of the composing underline
            gameTextField.ApplyComposition("abc");
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.Flex, underline.resolvedStyle.display);
            Assert.Greater(underline.resolvedStyle.width, 0f);
            Assert.AreEqual(1f, underline.resolvedStyle.height, 0.01f);
            Assert.AreEqual(gameTextField.Field.Q<TextElement>().resolvedStyle.color, underline.resolvedStyle.backgroundColor);
            var caretPanelPoint = gameTextField.Field.Q<TextElement>().LocalToWorld(gameTextField.Field.textSelection.cursorPosition);
            // 位置と幅はそれぞれ画素へ丸められるので、端は 1px までずれうる
            // Position and width are each snapped to pixels, so the edge may drift by up to 1px
            Assert.AreEqual(caretPanelPoint.x, underline.worldBound.xMax, 1f);
            Assert.AreEqual(caretPanelPoint.y, underline.worldBound.yMax, 1f);

            // 確定（未確定文字列が空）で消える
            // Committing (empty composition) hides it
            gameTextField.ApplyComposition(string.Empty);
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.None, underline.resolvedStyle.display);
        }

        [Test]
        public void SingleLineTextStaysMeasuredWithUnderlineAttached()
        {
            // 下線を TextElement の子にすると文字の計測が止まり、単行の文字の高さが 0 になって見えなくなる（段階 1 の実機で発見）
            // Parenting the underline to the TextElement stops text measuring and collapses single-line text to zero height (found on hardware in stage 1)
            var gameTextField = AddGameTextField(false);
            gameTextField.SetValueWithoutNotify("abc");
            simulate.FrameUpdate();

            var textElement = gameTextField.Field.Q<TextElement>();
            Assert.AreEqual(0, textElement.hierarchy.childCount, "TextElement に子がある（文字の計測が止まる）");
            Assert.Greater(textElement.layout.height, 0f);
        }

        [Test]
        public void FocusedFieldWritesCaretScreenPositionForCandidateWindow()
        {
            var gameTextField = AddGameTextField(false);
            gameTextField.Field.Focus();
            simulate.TypingText("abc");
            UnityEngine.Input.compositionCursorPos = Vector2.zero;
            simulate.FrameUpdate();

            // キャレット座標をピクセルへ換算
            // Convert the caret point to pixels
            var caretPanelPoint = gameTextField.Field.Q<TextElement>().LocalToWorld(gameTextField.Field.textSelection.cursorPosition);
            var expected = ImeCaretScreenPosition.FromPanel(caretPanelPoint, panel.visualTree.layout.size, new Vector2(Screen.width, Screen.height), ImeCaretScreenPosition.PlatformOrigin);
            Assert.AreNotEqual(Vector2.zero, expected);
            Assert.AreEqual(expected, UnityEngine.Input.compositionCursorPos);

            // 外れたら見張り停止
            // Watcher stops on focus loss
            gameTextField.Field.Blur();
            UnityEngine.Input.compositionCursorPos = Vector2.zero;
            simulate.FrameUpdate();
            Assert.AreEqual(Vector2.zero, UnityEngine.Input.compositionCursorPos);
        }

        [Test]
        public void TestIdIsOnInnerTextFieldAndOuterBoxIsInert()
        {
            var gameTextField = AddGameTextField(false);

            Assert.AreSame(gameTextField.Field, rootVisualElement.Q<UnityEngine.UIElements.TextField>(TestId));
            Assert.AreNotEqual(TestId, gameTextField.name);
            Assert.IsFalse(gameTextField.focusable);
            Assert.AreEqual(PickingMode.Ignore, gameTextField.pickingMode);
        }

        private GameTextField AddGameTextField(bool multiline)
        {
            var gameTextField = new GameTextField(TestId, multiline);
            gameTextField.style.width = 300;
            _rulesRoot.Add(gameTextField);
            simulate.FrameUpdate();
            return gameTextField;
        }
    }
}
