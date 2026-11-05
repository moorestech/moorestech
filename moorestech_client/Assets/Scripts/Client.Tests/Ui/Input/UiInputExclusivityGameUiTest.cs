using Client.Input;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Input
{
    // GameUi側の値とWeb側の値の論理和・ESCラッチ・ポインタprobeの登録を確かめる
    // Verifies the OR of GameUi and Web values, the ESC latch, and pointer-probe registration
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
            UiInputExclusivity.SetWebUiSuspended(false);
            UiInputExclusivity.SetState(false, false);
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.SetGameUiPointerProbe(null);
            UiInputExclusivity.ClearEscapeLatchForTest();
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

        [Test]
        public void WebFalseOverwriteKeepsGameUiFocus()
        {
            UiInputExclusivity.SetState(false, true);
            UiInputExclusivity.SetGameUiTextInputFocused(true);

            // Webがfalseを送ってもGameUiの欄があるのでtrueのまま
            // Web sending false keeps it true because the GameUi field still has focus
            UiInputExclusivity.SetState(false, false);
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void WebFocusAloneAndBothAreTextInputFocused()
        {
            UiInputExclusivity.SetState(false, true);
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);

            UiInputExclusivity.SetGameUiTextInputFocused(true);
            UiInputExclusivity.SetState(false, false);
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void SuspensionDoesNotAffectGameUiFocus()
        {
            UiInputExclusivity.SetGameUiTextInputFocused(true);
            UiInputExclusivity.SetWebUiSuspended(true);

            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
            Assert.IsTrue(UiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));

            // 休止解除でWebの古い値は捨てられ、GameUiの値だけが残る
            // Resuming drops the stale Web value and leaves only the GameUi value
            UiInputExclusivity.SetWebUiSuspended(false);
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void SuspendedWebFocusIsNotReadEvenIfGameUiToggles()
        {
            UiInputExclusivity.SetWebUiSuspended(true);
            UiInputExclusivity.SetState(true, true);
            UiInputExclusivity.SetGameUiTextInputFocused(true);
            UiInputExclusivity.SetGameUiTextInputFocused(false);

            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void EscapeLatchIsFalseBeforeAndTrueAfterConsume()
        {
            Assert.IsFalse(UiInputExclusivity.IsEscapeConsumedThisFrame);
            UiInputExclusivity.ConsumeEscapeThisFrame();
            Assert.IsTrue(UiInputExclusivity.IsEscapeConsumedThisFrame);
        }

        [Test]
        public void EscapeLatchedHybridInputReportsNoEscapePress()
        {
            // ラッチ中のHybridInputはESCの押下・保持を返さない（実キー注入での確認はL3）
            // While latched HybridInput reports no ESC press or hold (real key injection is covered by L3)
            UiInputExclusivity.ConsumeEscapeThisFrame();
            Assert.IsFalse(HybridInput.GetKeyDown(KeyCode.Escape));
            Assert.IsFalse(HybridInput.GetKey(KeyCode.Escape));
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
