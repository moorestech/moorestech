using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Toast;

namespace Client.Tests.Ui.State.Toast
{
    /// <summary>
    /// Commands がトーストへ渡した結果を名前つきで記録する偽物
    /// Fake that records the results Commands hand to the toaster, with their command names
    /// </summary>
    public sealed class RecordingUiCommandResultToaster : IUiCommandResultToaster
    {
        private readonly List<ReportedUiCommandResult> _reports = new();

        public IReadOnlyList<ReportedUiCommandResult> Reports => _reports;

        public void Report(string commandName, UiCommandResult result)
        {
            _reports.Add(new ReportedUiCommandResult(commandName, result));
        }
    }

    public readonly struct ReportedUiCommandResult
    {
        public readonly string CommandName;
        public readonly UiCommandResult Result;

        public ReportedUiCommandResult(string commandName, UiCommandResult result)
        {
            CommandName = commandName;
            Result = result;
        }
    }
}
