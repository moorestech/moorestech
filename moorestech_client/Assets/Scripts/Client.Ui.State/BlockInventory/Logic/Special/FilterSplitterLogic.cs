using System;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Logic.Special
{
    public enum FilterSlotClickAction
    {
        Set,
        Clear,
        Noop,
    }

    // フィルタ分配器の方向ごとのモード（Web の FilterSplitterMode）
    // Per-direction mode of the filter splitter (Web FilterSplitterMode)
    public enum FilterSplitterMode
    {
        Default,
        Whitelist,
        Blacklist,
    }

    /// <summary>
    /// フィルタ枠のクリックとモード文言（Web filterSplitterLogic.ts の写し）
    /// Filter slot clicks and mode labels (port of Web filterSplitterLogic.ts)
    /// </summary>
    public static class FilterSplitterLogic
    {
        // 消去が最優先。何も掴んでいない設定クリックは無操作
        // Clearing wins; an assign click with an empty grab does nothing
        public static FilterSlotClickAction ClickAction(int grabCount, bool clear)
        {
            if (clear) return FilterSlotClickAction.Clear;
            return grabCount > 0 ? FilterSlotClickAction.Set : FilterSlotClickAction.Noop;
        }

        public static LocalizationKey ModeKey(FilterSplitterMode mode)
        {
            return mode switch
            {
                FilterSplitterMode.Default => LocalizationKeys.Ui.BlockInventory.FilterDefault,
                FilterSplitterMode.Whitelist => LocalizationKeys.Ui.BlockInventory.FilterWhitelist,
                FilterSplitterMode.Blacklist => LocalizationKeys.Ui.BlockInventory.FilterBlacklist,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
            };
        }
    }
}
