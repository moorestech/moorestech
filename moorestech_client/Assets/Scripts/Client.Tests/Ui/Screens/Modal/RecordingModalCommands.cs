using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Modal;
using Client.Ui.State.Modal.Logic;

namespace Client.Tests.Ui.Screens.Modal
{
    /// <summary>
    /// View が返した応答を記録する偽（受理を返す）
    /// Records the replies the view returned (always accepts)
    /// </summary>
    internal sealed class RecordingModalCommands : IModalCommands
    {
        public readonly List<ModalResponse> Responses = new();

        public UiCommandResult Respond(ModalResponse response)
        {
            Responses.Add(response);
            return UiCommandAccepted.Instance;
        }
    }
}
