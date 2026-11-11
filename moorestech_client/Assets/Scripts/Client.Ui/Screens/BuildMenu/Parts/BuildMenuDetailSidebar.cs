using System;
using Client.Localization;
using Client.Ui.Components.Decor;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Icons;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.MaterialTooltip;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// 詳細の sticky（Web BuildMenuDetailSidebar の写し）。ヒントかエントリ 1 件を出す
    /// Sticky detail (port of the Web BuildMenuDetailSidebar); shows either the hint or one entry
    /// </summary>
    public sealed class BuildMenuDetailSidebar : VisualElement
    {
        private const int MaterialColumns = 3;
        private readonly GameContentPresentation _content;
        private readonly PlacementTargetIcons _icons;

        public BuildMenuDetailSidebar(GameContentPresentation content, PlacementTargetIcons icons)
        {
            _content = content;
            _icons = icons;
            name = "build-menu-detail";
            AddToClassList("build-menu__detail");
        }

        public void ShowHint()
        {
            Clear();
            Add(Label("build-menu__detail-hint", new UiText(LocalizationKeys.Ui.BuildMenu.DetailHint)));
        }

        public void ShowEntry(BuildMenuEntryViewModel entry)
        {
            Clear();

            // 絵 → 名前 → FadeRule → 素材 → 残り設置数（1 セット設置は Factory が Block のときだけ作る）
            // Picture, name, FadeRule, materials, remaining placements (the factory makes set placements for blocks only)
            if (_icons.TryToUiIcon(entry.Icon, out var icon)) Add(CreateIcon(icon));
            Add(Label("build-menu__detail-name", UiText.FromVerbatim(entry.Catalog.DisplayLabel)));
            Add(new FadeRule());
            switch (entry.SetPlacement)
            {
                case BuildMenuSetPlacementAvailable available:
                    AddMaterials(entry, new UiText(LocalizationKeys.Ui.BuildMenu.RequiredItemsPerSet, new[] { new LocalizationTextParam("count", available.PerCost.ToString()) }));
                    var remaining = Label("build-menu__detail-cost", new UiText(LocalizationKeys.Ui.BuildMenu.RemainingPlacementCount, new[] { new LocalizationTextParam("count", available.Remaining.ToString()) }));
                    remaining.name = "build-menu-remaining-placements";
                    Add(remaining);
                    break;
                case BuildMenuSetPlacementNone:
                    AddMaterials(entry, new UiText(LocalizationKeys.Ui.BuildMenu.RequiredItems));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entry.SetPlacement), entry.SetPlacement, null);
            }

            // Web の gap 8px は 2 つ目以降の子の上の余白で写す（UITK に gap が無い）
            // The Web 8px gap becomes a top margin on every child after the first (UITK has no gap)
            for (var i = 1; i < childCount; i++) this[i].AddToClassList("build-menu__detail-stacked");

            #region Internal

            VisualElement CreateIcon(UiIcon icon)
            {
                var gameIcon = new GameIcon();
                gameIcon.AddToClassList("build-menu__detail-icon");
                gameIcon.SetIcon(icon, GameIconFallback.None);
                return gameIcon;
            }

            #endregion
        }

        private void AddMaterials(BuildMenuEntryViewModel entry, UiText title)
        {
            // 素材の無いエントリは素材の欄を出さない（Web の条件描画と同じ正常な分岐）
            // Entries without materials show no materials section (a normal branch, same as the Web conditional render)
            if (entry.Materials.Count == 0) return;
            Add(Label("build-menu__detail-cost", title));

            // 不足は赤枠と所持/必要。免除中は赤くしない（VM が 1 回だけ判定した IsInsufficient）
            // Shortages get the red ring and owned/required; never red while payment is waived
            var grid = new SlotGrid("build-menu-detail-materials", MaterialColumns);
            for (var i = 0; i < entry.Materials.Count; i++)
            {
                var row = entry.Materials[i];
                var lacking = row.IsInsufficient;
                var tooltip = MaterialTooltipText.Create(MaterialTooltipKind.BuildMenuMaterial, _content.ItemName(row.ItemId).Resolve(), row.Item.Count, row.Item.Held);
                var slot = new ItemSlot($"build-menu-detail-material-{i}");
                // ghost・tooltipOverride の null 渡しは SlotItemContent.From と同じ
                // Passing null ghost and tooltipOverride follows SlotItemContent.From
                slot.SetContent(new ItemSlotContent(_content.Item(row.ItemId), null, new ItemSlotMarks(false, false, lacking), new ItemSlotShortage(row.Item.Held, row.Item.Count, lacking, tooltip), null, null));
                grid.AddItem(slot);
            }
            Add(grid);
        }

        private static LocalizedLabel Label(string className, UiText text)
        {
            var label = new LocalizedLabel();
            label.AddToClassList(UiTypographyClass.TextMd);
            label.AddToClassList(className);
            label.SetText(text);
            return label;
        }
    }
}
