using Client.Tests.Ui.State.Toast;
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
            var toaster = new RecordingUiCommandResultToaster();
            var commands = new ModalCommands(service, toaster);

            commands.Respond(ModalResponse.Confirm(service.Pending, ""));
            Assert.AreEqual(1, toaster.Reports.Count);
            Assert.AreEqual(ModalCommands.RespondCommandName, toaster.Reports[0].CommandName);
            Assert.AreSame(UiCommandAccepted.Instance, toaster.Reports[0].Result);
            Assert.AreEqual(UniTaskStatus.Succeeded, result.Status);
            Assert.IsNull(service.Pending);
        }

        [Test]
        public void 閉じた後の古い応答は良性の拒否になる()
        {
            var service = new ModalRequestService();
            service.RequestModal("確認", "本文", "OK", ModalVariant.Confirm).Forget();
            var request = service.Pending;
            var toaster = new RecordingUiCommandResultToaster();
            var commands = new ModalCommands(service, toaster);
            commands.Respond(ModalResponse.Cancel(request));

            commands.Respond(ModalResponse.Cancel(request));
            Assert.AreEqual(2, toaster.Reports.Count);
            var rejected = (UiCommandRejected)toaster.Reports[1].Result;
            Assert.AreEqual(UiCommandRejectReason.ModalResponseStale, rejected.Reason);
            Assert.AreEqual("no_pending_modal", rejected.ToWireCode());
            Assert.IsTrue(UiCommandRejectReasonTable.Get(rejected.Reason).IsBenign);
        }
    }
}
