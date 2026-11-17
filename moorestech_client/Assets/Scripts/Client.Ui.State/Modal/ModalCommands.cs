using Client.Ui.State.Commands;
using Client.Ui.State.Modal.Logic;
using Client.Ui.State.Toast;

namespace Client.Ui.State.Modal
{
    /// <summary>
    /// 応答の本番実装。正は ModalRequestService
    /// Production reply; the source of truth is ModalRequestService
    /// </summary>
    public sealed class ModalCommands : IModalCommands
    {
        public const string RespondCommandName = "ui.modal.respond";

        private readonly ModalRequestService _service;
        private readonly IUiCommandResultToaster _toaster;

        public ModalCommands(ModalRequestService service, IUiCommandResultToaster toaster)
        {
            _service = service;
            _toaster = toaster;
        }

        // ID 不一致・保留無しは古い応答。状態を変えないので良性の拒否（ログはトースト口）
        // An id mismatch or no pending request is a stale reply; it changes nothing, so it is a benign rejection (the toaster logs it)
        public void Respond(ModalResponse response)
        {
            var result = _service.Respond(response.RequestId, response.Result, response.Text) ? (UiCommandResult)UiCommandAccepted.Instance : UiCommandRejected.Of(UiCommandRejectReason.ModalResponseStale);
            _toaster.Report(RespondCommandName, result);
        }
    }
}
