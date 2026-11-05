using System.Collections.Generic;
using Client.Input;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.Ui.Input
{
    // Web全面休止（R1）の間はWeb由来の入力占有を一切認めず、解除後も古い値を残さない
    // While the web UI is fully suspended (R1) no web-originated input claim survives, and none lingers after resuming
    public class UiInputExclusivityWebSuspensionTest
    {
        [TearDown]
        public void TearDown()
        {
            UiInputExclusivity.SetWebUiSuspended(false);
            UiInputExclusivity.SetState(false, false);
        }

        [Test]
        public void SuspensionDropsStateReceivedWhileSuspended()
        {
            UiInputExclusivity.SetWebUiSuspended(true);
            UiInputExclusivity.SetState(true, true);

            Assert.IsFalse(UiInputExclusivity.IsPointerOverWebUi);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
            Assert.IsFalse(UiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));

            // 解除しても休止中に届いた値は復活しない。Webの次の送信を待つ
            // Resuming does not revive values received while suspended; the web's next send is awaited
            UiInputExclusivity.SetWebUiSuspended(false);
            Assert.IsFalse(UiInputExclusivity.IsPointerOverWebUi);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);

            UiInputExclusivity.SetState(true, true);
            Assert.IsTrue(UiInputExclusivity.IsPointerOverWebUi);
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void SuspensionClearsFocusAndNotifiesSubscribers()
        {
            UiInputExclusivity.SetState(true, true);
            Assert.IsTrue(UiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));

            var received = new List<bool>();
            using var subscription = UiInputExclusivity.TextInputFocused.Subscribe(received.Add);

            UiInputExclusivity.SetWebUiSuspended(true);

            // 購読者にも false が届き、キー抑止も外れる
            // Subscribers receive false and keyboard suppression is lifted
            CollectionAssert.AreEqual(new[] { true, false }, received);
            Assert.IsFalse(UiInputExclusivity.IsPointerOverWebUi);
            Assert.IsFalse(UiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));
        }
    }
}
