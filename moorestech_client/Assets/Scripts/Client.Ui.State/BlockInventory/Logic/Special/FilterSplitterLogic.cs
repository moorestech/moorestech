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

    // フィルター分岐器の出口の側（入口から見て）。画面の層は Game.Block.Interface を参照しないので、サーバーの列挙を SpecialNetworkReader.ToClientSide で写す
    // Exit side of the filter splitter seen from the entry; the UI layer does not reference Game.Block.Interface, so SpecialNetworkReader.ToClientSide maps the server enum
    public enum FilterSplitterOutputSide
    {
        Left,
        Front,
        Right,
    }

    /// <summary>
    /// フィルタ枠のクリック・モード名・出口の側の文言
    /// Filter slot clicks, mode labels and exit side labels
    /// </summary>
    public static class FilterSplitterLogic
    {
        // 消去が最優先。何も掴んでいない設定クリックは無操作
        // Clearing wins; an assign click with an empty grab does nothing
        public static FilterSlotClickAction ClickAction(int grabCount, bool clear)
        {
            if (clear) return FilterSlotClickAction.Clear;
            return 0 < grabCount ? FilterSlotClickAction.Set : FilterSlotClickAction.Noop;
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

        // 出口の見出し。並びは方向の番号のままで、文は入口から見た側で決まる（ユーザー裁定 2026-11-19）
        // Exit headings keep the direction order; the text follows the side seen from the entry (user decision 2026-11-19)
        public static LocalizationKey OutputSideKey(FilterSplitterOutputSide side)
        {
            return side switch
            {
                FilterSplitterOutputSide.Left => LocalizationKeys.Ui.BlockInventory.FilterOutputLeft,
                FilterSplitterOutputSide.Front => LocalizationKeys.Ui.BlockInventory.FilterOutputFront,
                FilterSplitterOutputSide.Right => LocalizationKeys.Ui.BlockInventory.FilterOutputRight,
                _ => throw new ArgumentOutOfRangeException(nameof(side), side, null),
            };
        }
    }
}
