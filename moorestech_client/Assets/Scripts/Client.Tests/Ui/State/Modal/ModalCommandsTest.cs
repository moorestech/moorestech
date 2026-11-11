using Client.Ui.State.Commands;
using Client.Ui.State.Modal;
using Client.Ui.State.Modal.Logic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Modal
{
    /// <summary>
    /// モーダルの応答の操作。保留への応答は受理、古い応答は良性の拒否（Web ui.modal.respond の no_pending_modal と同じ）
    /// The modal reply command: a reply to the pending request is accepted, a stale one is a benign rejection (same as Web no_pending_modal)
    /// </summary>
    public class ModalCommandsTest
    {
        [Test]
        public void 保留中の要求への確定は受理され要求が閉じる()
        {
            var service = new ModalRequestService();
            var result = service.RequestModal("確認", "本文", "OK", ModalVariant.Confirm);
            var commands = new ModalCommands(service);

            Assert.AreSame(UiCommandAccepted.Instance, commands.Respond(ModalResponse.Confirm(service.Pending, "")));
            Assert.AreEqual(UniTaskStatus.Succeeded, result.Status);
            Assert.IsNull(service.Pending);
        }

        [Test]
        public void 閉じた後の古い応答は良性の拒否になる()
        {
            var service = new ModalRequestService();
            service.RequestModal("確認", "本文", "OK", ModalVariant.Confirm).Forget();
            var request = service.Pending;
            var commands = new ModalCommands(service);
            commands.Respond(ModalResponse.Cancel(request));

            var rejected = (UiCommandRejected)commands.Respond(ModalResponse.Cancel(request));
            Assert.AreEqual(UiCommandRejectReason.ModalResponseStale, rejected.Reason);
            Assert.AreEqual("no_pending_modal", rejected.ToWireCode());
            Assert.IsTrue(UiCommandRejectReasonTraits.IsBenign(rejected.Reason));
        }
    }
}
