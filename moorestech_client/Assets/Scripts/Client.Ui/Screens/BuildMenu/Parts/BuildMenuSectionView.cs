using System;
using System.Collections.Generic;
using Client.Ui.Components.Decor;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.BuildMenu.Logic;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// サブカテゴリ節（小見出し → FadeRule → 8 列の格子。Web BuildMenuCategoryGrid の写し）
    /// One sub-category section: heading, FadeRule, 8-column grid (port of the Web BuildMenuCategoryGrid)
    /// </summary>
    public sealed class BuildMenuSectionView : VisualElement
    {
        private const int Columns = 8;
        private readonly List<BuildMenuEntrySlot> _slots = new();

        public BuildMenuSectionView(BuildMenuSection section, BuildMenuViewModel viewModel, GameContentPresentation content, PlacementTargetIcons icons, IBuildMenuEntryHandler handler)
        {
            name = $"build-menu-section-{section.CategoryGuid:D}-{section.SubCategoryGuid:D}";
            AddToClassList("build-menu__section");

            var heading = new LocalizedLabel();
            heading.AddToClassList("build-menu__section-heading");
            heading.SetText(viewModel.HeadingNameOf(section.SubCategoryGuid));
            Add(heading);
            var rule = new FadeRule();
            rule.AddToClassList("build-menu__stacked");
            Add(rule);

            // 格子へエントリのマスを並べる
            // Lay the entry slots into the grid
            var grid = new SlotGrid($"build-menu-grid-{section.CategoryGuid:D}-{section.SubCategoryGuid:D}", Columns);
            grid.AddToClassList("build-menu__stacked");
            foreach (var catalog in section.Entries)
            {
                var slot = new BuildMenuEntrySlot(EntryOf(viewModel, catalog.TargetId), content, icons, handler);
                _slots.Add(slot);
                grid.AddItem(slot.Frame);
            }
            Add(grid);
        }

        // 構造が同じときの差し替え。並びは BuildMenuCategoryList が構造の鍵で保証する
        // Swap contents when the structure is unchanged; BuildMenuCategoryList guarantees the order through its structure key
        public void Render(BuildMenuSection section, BuildMenuViewModel viewModel)
        {
            for (var i = 0; i < _slots.Count; i++) _slots[i].Render(EntryOf(viewModel, section.Entries[i].TargetId));
        }

        // 群は同じ VM の CatalogEntries から作るので必ず引ける。引けないのは組み立ての誤り
        // Groups come from the same view model's CatalogEntries, so lookup always succeeds; a miss is a construction bug
        private static BuildMenuEntryViewModel EntryOf(BuildMenuViewModel viewModel, Guid targetId)
        {
            if (!viewModel.TryGetEntry(targetId, out var entry)) throw new InvalidOperationException($"[BuildMenu] entry {targetId} is not in the view model");
            return entry;
        }
    }
}
