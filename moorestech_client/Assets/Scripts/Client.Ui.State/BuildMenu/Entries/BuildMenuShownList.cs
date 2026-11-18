using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.BuildMenu.Logic;

namespace Client.Ui.State.BuildMenu.Entries
{
    /// <summary>
    /// 検索後の群・サイド項目・該当なし
    /// What to show now: searched groups, sidebar items with their enabled state, and the no-results flag
    /// </summary>
    public sealed class BuildMenuShownList
    {
        public IReadOnlyList<BuildMenuCategoryGroup> Groups { get; }
        public IReadOnlyList<Guid> SidebarCategories { get; }
        public IReadOnlyList<Guid> ShownCategoryGuids { get; }
        public bool ShowsNoResults { get; }

        private readonly HashSet<Guid> _shown;

        public BuildMenuShownList(IReadOnlyList<BuildMenuCategoryGroup> groups, IReadOnlyList<Guid> sidebarCategories, bool showsNoResults)
        {
            Groups = groups;
            SidebarCategories = sidebarCategories;
            ShownCategoryGuids = groups.Select(group => group.CategoryGuid).ToList();
            ShowsNoResults = showsNoResults;
            _shown = new HashSet<Guid>(ShownCategoryGuids);
        }

        public bool IsCategoryEnabled(Guid categoryGuid)
        {
            return _shown.Contains(categoryGuid);
        }
    }
}
