using System;
using System.Collections.Generic;

namespace Client.Ui.State.Challenge.Tree
{
    /// <summary>
    /// 解放済みのカテゴリの列と、その中の選んだカテゴリ（必ずある）
    /// Unlocked categories and the selected one among them (always present)
    /// </summary>
    public sealed class SelectedChallengeListViewModel : ChallengeListViewModel
    {
        public IReadOnlyList<ChallengeCategoryViewModel> Categories { get; }
        public IReadOnlyList<Guid> CategoryGuids { get; }
        public ChallengeCategoryViewModel Selected { get; }

        internal SelectedChallengeListViewModel(IReadOnlyList<ChallengeCategoryViewModel> categories, ChallengeCategoryViewModel selected)
        {
            Categories = categories;
            Selected = selected;

            // ボタン列の比較に渡す Guid の列
            // Guid sequence for the button row comparison
            var guids = new List<Guid>(categories.Count);
            foreach (var category in categories) guids.Add(category.Guid);
            CategoryGuids = guids;
        }
    }
}
