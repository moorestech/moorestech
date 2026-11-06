using Client.Input;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Input
{
    // GameUi の系の入力占有を確かめる
    // Verifies input occupancy in the GameUi system
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
            // GameUi では Web の値を読まない
            // GameUi does not read web values
            UiInputExclusivity.SetState(false, true);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
            Assert.IsFalse(UiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));

            // Web の false で GameUi の欄は残る
            // Web false keeps the GameUi field focus
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
