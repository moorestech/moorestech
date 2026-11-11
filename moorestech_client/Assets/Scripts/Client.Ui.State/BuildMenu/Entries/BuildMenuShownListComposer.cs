using System.Linq;
using Client.Ui.State.BuildMenu.Logic;

namespace Client.Ui.State.BuildMenu.Entries
{
    /// <summary>
    /// Web BuildMenuPanel の allGroups・shownGroups・sidebarItems・noHit の写し
    /// Port of allGroups, shownGroups, sidebarItems and noHit in the Web BuildMenuPanel
    /// </summary>
    public static class BuildMenuShownListComposer
    {
        public static BuildMenuShownList Compose(BuildMenuViewModel viewModel, string query)
        {
            // サイドバーは全エントリの群、本文は検索を当てた群
            // The sidebar lists groups of all entries; the body lists the searched groups
            var allGroups = BuildMenuGrouping.Group(viewModel.Categories, viewModel.CatalogEntries);
            var searching = query.Length > 0;
            var shownGroups = searching ? BuildMenuGrouping.Group(viewModel.Categories, BuildMenuGrouping.Search(query, viewModel.CatalogEntries)) : allGroups;
            var sidebar = allGroups.Select(group => group.CategoryGuid).ToList();
            return new BuildMenuShownList(shownGroups, sidebar, searching && shownGroups.Count == 0);
        }
    }
}
