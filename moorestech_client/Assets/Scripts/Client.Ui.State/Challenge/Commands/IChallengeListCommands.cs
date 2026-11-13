using System;

namespace Client.Ui.State.Challenge.Commands
{
    /// <summary>
    /// チャレンジ一覧の画面操作（前例 IResearchTreeCommands）
    /// Challenge list screen operations (precedent IResearchTreeCommands)
    /// </summary>
    public interface IChallengeListCommands
    {
        void SelectCategory(Guid categoryGuid);
    }
}
