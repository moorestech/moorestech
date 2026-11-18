using Client.Game.InGame.Control;
using Client.Input;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Input
{
    // GameUi の入力占有を確かめる
    // Verifies the GameUi input occupancy
    public class UiInputExclusivityGameUiTest
    {
        private class FixedProbe : IGameUiPointerProbe
        {
            private readonly bool _result;
            public FixedProbe(bool result) { _result = result; }
            public bool IsPointerOverGameUi(Vector2 screenPosition) => _result;
        }

        [TearDown]
        public void TearDown()
        {
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.SetGameUiPointerProbe(null);
            UiInputExclusivity.ClearEscapeLatchForTest();
            InputManager.MouseCursorVisible(true);
        }

        [Test]
        public void GameUiFocusAloneIsTextInputFocused()
        {
            UiInputExclusivity.SetGameUiTextInputFocused(true);

            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
            Assert.IsTrue(UiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
        }

        // 判定はロック状態の正本を読む。batchmode でも Cursor.lockState を読まないので CI で効く（UiInputExclusivityActiveSystemTest から移した）
        // The check reads the lock-state source of truth, so it holds in batchmode CI where Cursor.lockState stays None (moved from UiInputExclusivityActiveSystemTest)
        [Test]
        public void ロック中はUIの上に乗っていても乗っていないと答える()
        {
            UiInputExclusivity.SetGameUiPointerProbe(new FixedProbe(true));
            InputManager.MouseCursorVisible(false);
            Assert.IsFalse(UiPointerHitTest.IsPointerOverAnyUi());

            InputManager.MouseCursorVisible(true);
            Assert.IsTrue(UiPointerHitTest.IsPointerOverAnyUi());
        }

        [Test]
        public void EscapeLatchIsFalseBeforeAndTrueAfterConsume()
        {
            Assert.IsFalse(UiInputExclusivity.IsEscapeConsumedThisFrame);
            UiInputExclusivity.ConsumeEscapeThisFrame();
            Assert.IsTrue(UiInputExclusivity.IsEscapeConsumedThisFrame);
        }

        /// <web-test file="src/shared/uiState/activeLayer.test.ts">updates pointer and text focus as independent axes</web-test>
        [Test]
        public void TextFocusAndPointerOccupancyAreIndependentAxes()
        {
            // 文字入力の旗を立ててもポインタの占有は変わらない
            // Raising the text-input flag leaves pointer occupancy untouched
            UiInputExclusivity.SetGameUiTextInputFocused(true);
            Assert.IsFalse(UiInputExclusivity.IsPointerOverGameUi(Vector2.zero));

            // ポインタが乗っても文字入力の旗は保たれる
            // The text-input flag survives the pointer coming over the UI
            UiInputExclusivity.SetGameUiPointerProbe(new FixedProbe(true));
            Assert.IsTrue(UiInputExclusivity.IsPointerOverGameUi(Vector2.zero));
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);

            // 旗を下ろしてもポインタは乗ったまま
            // Lowering the flag keeps the pointer over the UI
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            Assert.IsTrue(UiInputExclusivity.IsPointerOverGameUi(Vector2.zero));
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void PointerProbeRegistrationAndRelease()
        {
            Assert.IsFalse(UiInputExclusivity.IsPointerOverGameUi(Vector2.zero));

            UiInputExclusivity.SetGameUiPointerProbe(new FixedProbe(true));
            Assert.IsTrue(UiInputExclusivity.IsPointerOverGameUi(Vector2.zero));

            UiInputExclusivity.SetGameUiPointerProbe(new FixedProbe(false));
            Assert.IsFalse(UiInputExclusivity.IsPointerOverGameUi(Vector2.zero));

            UiInputExclusivity.SetGameUiPointerProbe(null);
            Assert.IsFalse(UiInputExclusivity.IsPointerOverGameUi(Vector2.zero));
        }
    }
}
