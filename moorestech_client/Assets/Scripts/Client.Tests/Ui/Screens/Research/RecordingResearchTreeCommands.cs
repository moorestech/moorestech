using System;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.Commands;
using Client.Ui.State.Research.Commands;
using Cysharp.Threading.Tasks;

namespace Client.Tests.Ui.Screens.Research
{
    /// <summary>
    /// 研究操作を記録する偽
    /// Fake recording research operations
    /// </summary>
    internal sealed class RecordingResearchTreeCommands : CommandRecorder<string>, IResearchTreeCommands
    {
        private UiCommandResult _completeResult = UiCommandAccepted.Instance;

        public void SetCompleteResult(UiCommandResult result)
        {
            _completeResult = result;
        }

        public void ToggleNodeSelection(Guid researchGuid)
        {
            Record($"toggle:{researchGuid}");
        }

        public void CloseDetail()
        {
            Record("close");
        }

        public UniTask<UiCommandResult> CompleteResearch(Guid researchGuid)
        {
            Record($"complete:{researchGuid}");
            return UniTask.FromResult(_completeResult);
        }
    }
}
