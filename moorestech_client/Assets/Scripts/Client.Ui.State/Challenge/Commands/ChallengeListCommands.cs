using System;
using Client.Ui.State.Challenge.Tree.Selection;

namespace Client.Ui.State.Challenge.Commands
{
    /// <summary>
    /// チャレンジ一覧の操作（カテゴリの選択を選択ストアへ）
    /// Challenge list operations (category selection goes to the selection store)
    /// </summary>
    public sealed class ChallengeListCommands : IChallengeListCommands
    {
        private readonly ChallengeCategorySelectionStore _selection;

        public ChallengeListCommands(ChallengeCategorySelectionStore selection)
        {
            _selection = selection;
        }

        public void SelectCategory(Guid categoryGuid)
        {
            _selection.Select(categoryGuid);
        }
    }
}
