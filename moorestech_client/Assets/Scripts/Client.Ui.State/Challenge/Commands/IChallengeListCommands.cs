using System;

namespace Client.Ui.State.Challenge.Commands
{
    /// <summary>
    /// チャレンジ一覧の画面からの操作（サーバーへは送らない。前例 IResearchTreeCommands）
    /// Operations from the challenge list screen (nothing is sent to the server; precedent IResearchTreeCommands)
    /// </summary>
    public interface IChallengeListCommands
    {
        void SelectCategory(Guid categoryGuid);
    }
}
