using Client.Input;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Ui.Foundation.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BuildMenu
{
    /// <summary>
    /// 検索欄の入力中はゲームのキーを止め、ESC はフォーカスを外すだけ（L1・画面側）
    /// While typing in search the game keys stop, and ESC only drops focus (L1, screen side)
    /// </summary>
    public class BuildMenuSearchInputGuardTest : BuildMenuScreenTestHost
    {
        [SetUp]
        public void AttachRulesAndOpen()
        {
            // 規則は画面の要素に付け、基底の Clear で画面ごと捨てる（前例 GameUiInputRulesTest）
            // Attach the rules to the screen element so the base Clear discards them (precedent GameUiInputRulesTest)
            GameUiInputRules.Attach(View);
            OpenMenu(BuildMenuSamples.Create());
        }

        [TearDown]
        public void ResetExclusivity()
        {
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.ClearEscapeLatchForTest();
        }

        [Test]
        public void 検索欄にフォーカスするとキーボードのゲーム入力が止まる()
        {
            SearchField().Focus();
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
            Assert.IsTrue(UiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));
        }

        [Test]
        public void 検索欄のESCはフォーカスを外すだけで画面と検索語は残る()
        {
            var field = SearchField();
            field.Focus();
            simulate.TypingText("chest");
            simulate.KeyDown(KeyCode.Escape);

            Assert.IsTrue(UiInputExclusivity.IsEscapeConsumedThisFrame);
            Assert.IsNull(panel.focusController.focusedElement);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
            Assert.AreEqual(DisplayStyle.Flex, View.resolvedStyle.display);
            Assert.AreEqual("chest", field.value);
        }

        [Test]
        public void 検索欄にフォーカスしたまま画面を閉じると入力中の旗が落ちる()
        {
            // 申し送り: 入力欄入りの View をフォーカスごと外すと旗が残る問題がモーダル以外にもあるか
            // Hand-off: does hiding a view with a focused field leave the flag set outside the modal too
            SearchField().Focus();
            CloseMenu();
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
        }

        private TextField SearchField()
        {
            return Find<TextField>(View, "build-menu-search");
        }
    }
}
