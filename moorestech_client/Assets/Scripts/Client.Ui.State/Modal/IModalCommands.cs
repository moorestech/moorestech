using Client.Ui.State.Modal.Logic;

namespace Client.Ui.State.Modal
{
    /// <summary>
    /// モーダルの応答の操作。結果は実装がトーストへ渡す
    /// The modal reply command; the implementation hands the result to the toaster
    /// </summary>
    public interface IModalCommands
    {
        void Respond(ModalResponse response);
    }
}
