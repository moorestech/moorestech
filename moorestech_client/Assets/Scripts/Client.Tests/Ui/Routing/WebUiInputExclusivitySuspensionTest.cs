using System.Collections.Generic;
using Client.Input;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.Ui.Routing
{
    // Web全面休止（R1）の間はWeb由来の入力占有を一切認めず、解除後も古い値を残さない
    // While the web UI is fully suspended (R1) no web-originated input claim survives, and none lingers after resuming
    public class WebUiInputExclusivitySuspensionTest
    {
        [TearDown]
        public void TearDown()
        {
            WebUiInputExclusivity.SetWebUiSuspended(false);
            WebUiInputExclusivity.SetState(false, false);
        }

        [Test]
        public void SuspensionDropsStateReceivedWhileSuspended()
        {
            WebUiInputExclusivity.SetWebUiSuspended(true);
            WebUiInputExclusivity.SetState(true, true);

            Assert.IsFalse(WebUiInputExclusivity.IsPointerOverWebUi);
            Assert.IsFalse(WebUiInputExclusivity.IsTextInputFocused);
            Assert.IsFalse(WebUiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));

            // 解除しても休止中に届いた値は復活しない。Webの次の送信を待つ
            // Resuming does not revive values received while suspended; the web's next send is awaited
            WebUiInputExclusivity.SetWebUiSuspended(false);
            Assert.IsFalse(WebUiInputExclusivity.IsPointerOverWebUi);
            Assert.IsFalse(WebUiInputExclusivity.IsTextInputFocused);

            WebUiInputExclusivity.SetState(true, true);
            Assert.IsTrue(WebUiInputExclusivity.IsPointerOverWebUi);
            Assert.IsTrue(WebUiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void SuspensionClearsFocusAndNotifiesSubscribers()
        {
            WebUiInputExclusivity.SetState(true, true);
            Assert.IsTrue(WebUiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));

            var received = new List<bool>();
            using var subscription = WebUiInputExclusivity.TextInputFocused.Subscribe(received.Add);

            WebUiInputExclusivity.SetWebUiSuspended(true);

            // 購読者にも false が届き、キー抑止も外れる
            // Subscribers receive false and keyboard suppression is lifted
            CollectionAssert.AreEqual(new[] { true, false }, received);
            Assert.IsFalse(WebUiInputExclusivity.IsPointerOverWebUi);
            Assert.IsFalse(WebUiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));
        }
    }
}
