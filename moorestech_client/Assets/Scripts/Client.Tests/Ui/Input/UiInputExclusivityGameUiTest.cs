using Client.Input;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Input
{
    // GameUi の系での文字入力・ESCラッチ・ポインタprobeの登録を確かめる。Web の値は読まない（plan §3.3 R2）
    // Verifies text input, the ESC latch and pointer-probe registration in the GameUi system; web values are not read (plan §3.3 R2)
    public class UiInputExclusivityGameUiTest
    {
        private class FixedProbe : IGameUiPointerProbe
        {
            private readonly bool _result;
            public FixedProbe(bool result) { _result = result; }
            public bool IsPointerOverGameUi(Vector2 screenPosition) => _result;
        }

        [SetUp]
        public void SetUp()
        {
            UiInputExclusivity.SetActiveUiSystem(UiSystem.GameUi);
        }

        [TearDown]
        public void TearDown()
        {
            UiInputExclusivity.SetState(false, false);
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.SetGameUiPointerProbe(null);
            UiInputExclusivity.ClearEscapeLatchForTest();
            UiInputExclusivity.SetActiveUiSystem(UiSystem.Web);
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
        public void WebFocusIsNotReadInGameUiSystem()
        {
            // Web が入力欄フォーカスを送っても GameUi の系では読まない
            // A web text-field focus is not read in the GameUi system
            UiInputExclusivity.SetState(false, true);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
            Assert.IsFalse(UiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));

            // Web が false を送っても GameUi の欄のフォーカスは残る
            // Web sending false keeps the GameUi field's focus
            UiInputExclusivity.SetGameUiTextInputFocused(true);
            UiInputExclusivity.SetState(false, false);
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void EscapeLatchIsFalseBeforeAndTrueAfterConsume()
        {
            Assert.IsFalse(UiInputExclusivity.IsEscapeConsumedThisFrame);
            UiInputExclusivity.ConsumeEscapeThisFrame();
            Assert.IsTrue(UiInputExclusivity.IsEscapeConsumedThisFrame);
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
