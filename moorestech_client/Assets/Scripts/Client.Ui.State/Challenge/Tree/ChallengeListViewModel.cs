using System.Collections.Generic;
using Client.Ui.State.Challenge.Tree.Selection;

namespace Client.Ui.State.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧の表示（空か選択ありの 2 型）
    /// Challenge list display (empty or with a selection)
    /// </summary>
    /// <remarks>
    /// 選択なしを bool と null の 2 つで持たないため、型で分ける（前例 BlockInventoryPanelState と ClosedBlockInventoryPanel）。派生はこのアセンブリの 2 つに閉じる
    /// Split by type so "no selection" is never held as both a bool and a null (precedent BlockInventoryPanelState and ClosedBlockInventoryPanel); derivation is closed to the two in this assembly
    /// </remarks>
    public abstract class ChallengeListViewModel
    {
        public static readonly ChallengeListViewModel Empty = new EmptyChallengeListViewModel();

        private protected ChallengeListViewModel()
        {
        }

        public static ChallengeListViewModel Create(IReadOnlyList<ChallengeCategoryViewModel> categories, ChallengeCategorySelection selection)
        {
            // 解放済みが無ければ空。選択が無ければ先頭
            // Empty without unlocked categories; no selection uses the first
            if (categories.Count == 0) return Empty;
            foreach (var category in categories)
            {
                if (selection.Is(category.Guid)) return new SelectedChallengeListViewModel(categories, category);
            }
            return new SelectedChallengeListViewModel(categories, categories[0]);
        }
    }
}
