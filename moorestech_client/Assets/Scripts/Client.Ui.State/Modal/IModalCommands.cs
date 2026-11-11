using Client.Ui.State.Commands;
using Client.Ui.State.Modal.Logic;

namespace Client.Ui.State.Modal
{
    /// <summary>
    /// モーダルの応答の操作
    /// The modal reply command
    /// </summary>
    public interface IModalCommands
    {
        UiCommandResult Respond(ModalResponse response);
    }
}
