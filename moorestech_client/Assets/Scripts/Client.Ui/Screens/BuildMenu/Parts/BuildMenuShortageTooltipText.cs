using System.Collections.Generic;
using Client.Ui.Content;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.MaterialTooltip;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.Screens.BuildMenu.Parts
{
    /// <summary>
    /// 不足ツールチップの文。題と不足行を改行で結ぶ（Web BuildMenuSlot の pre-line の写し）
    /// Shortage tooltip text: the title and shortage lines joined by newlines (port of the Web BuildMenuSlot pre-line)
    /// </summary>
    public static class BuildMenuShortageTooltipText
    {
        // 不足が無ければ偽（印を付けない。前例 PlacementTargetIcons.TryToUiIcon の形）
        // False when nothing is short (no mark; same shape as PlacementTargetIcons.TryToUiIcon)
        public static bool TryCreate(BuildMenuEntryViewModel entry, GameContentPresentation content, out UiText text)
        {
            text = null;
            var lines = new List<string> { new UiText(LocalizationKeys.Ui.BuildMenu.MaterialShortageTitle).Resolve() };
            foreach (var row in entry.Materials)
            {
                if (!row.IsInsufficient) continue;
                var itemName = content.ItemName(row.Row.ItemId).Resolve();
                lines.Add(MaterialTooltipText.Create(MaterialTooltipKind.BuildMenuShortageLine, itemName, row.Row.Item.Count, row.Row.Item.Held).Resolve());
            }
            if (lines.Count == 1) return false;
            text = UiText.FromVerbatim(string.Join("\n", lines));
            return true;
        }
    }
}
