using System.Collections.Generic;
using Client.Ui.State.Challenge.Tree.Selection;

namespace Client.Ui.State.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧の表示。解放済みのカテゴリが無い空（EmptyChallengeListViewModel）か、選んだカテゴリを必ず持つ（SelectedChallengeListViewModel）かの 2 つだけ。等価なし
    /// Challenge list display: only empty with no unlocked category (EmptyChallengeListViewModel) or always holding the selected one (SelectedChallengeListViewModel); no equality
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
            // 解放済みが無ければ空。選んだカテゴリが出ていればそれ、無ければ先頭（Web の find(...) ?? categories[0]）
            // Empty without an unlocked category; otherwise the selected one when listed, else the first (the Web find(...) ?? categories[0])
            if (categories.Count == 0) return Empty;
            foreach (var category in categories)
            {
                if (selection.Is(category.Guid)) return new SelectedChallengeListViewModel(categories, category);
            }
            return new SelectedChallengeListViewModel(categories, categories[0]);
        }
    }
}
