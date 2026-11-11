using Client.Ui.State.Commands;
using Client.Ui.State.Modal.Logic;

namespace Client.Ui.State.Modal
{
    /// <summary>
    /// モーダルの応答の操作（Web の ui.modal.respond。設計書 A-8）
    /// The modal reply command (Web ui.modal.respond; design A-8)
    /// </summary>
    public interface IModalCommands
    {
        UiCommandResult Respond(ModalResponse response);
    }
}
