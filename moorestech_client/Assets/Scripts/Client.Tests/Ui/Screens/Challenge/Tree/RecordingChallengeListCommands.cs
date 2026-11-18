using System;
using System.Collections.Generic;
using Client.Ui.State.Challenge.Commands;

namespace Client.Tests.Ui.Screens.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧の操作を記録するだけの差し替え
    /// Test double that only records challenge list operations
    /// </summary>
    internal sealed class RecordingChallengeListCommands : IChallengeListCommands
    {
        public List<Guid> Selected { get; } = new();

        public void SelectCategory(Guid categoryGuid)
        {
            Selected.Add(categoryGuid);
        }
    }
}
