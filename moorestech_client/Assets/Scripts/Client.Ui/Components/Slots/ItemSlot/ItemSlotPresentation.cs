using Client.Ui.Foundation.Icons;
using Client.Ui.State.Text;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// 入力値から面・バッジ・ツールチップを決める（Web の式）
    /// Derives face, badge and tooltip from the input (the Web formulas)
    /// </summary>
    internal sealed class ItemSlotPresentation
    {
        public bool Filled { get; }
        public bool GhostShown { get; }
        public UiIcon ShownIcon { get; }
        public bool ShowsBadge { get; }
        public int BadgeCount { get; }
        public UiText Tooltip { get; }

        private ItemSlotPresentation(bool filled, bool ghostShown, UiIcon shownIcon, bool showsBadge, int badgeCount, UiText tooltip)
        {
            Filled = filled;
            GhostShown = ghostShown;
            ShownIcon = shownIcon;
            ShowsBadge = showsBadge;
            BadgeCount = badgeCount;
            Tooltip = tooltip;
        }

        public static ItemSlotPresentation From(ItemSlotContent content)
        {
            // 白面は所持数がある時だけ（Web の owned）
            // White face only when owned (the Web owned)
            var count = content.Count;
            var owned = count.HasValue && 0 < count.Value;
            var hasItem = content.Icon != null && (content.Marks.Catalog || !count.HasValue || 0 < count.Value);
            var filled = content.Marks.Catalog ? owned : hasItem;

            // 実物の無い空きマスに限りゴーストを描く。白面は実物だけで決める
            // The ghost draws only on a genuinely empty cell; the white face is decided by the real item alone
            var ghostShown = content.Ghost != null && !hasItem;
            var shownIcon = ghostShown ? content.Ghost.Icon : hasItem ? content.Icon : null;
            var shownCount = ghostShown ? content.Ghost.Count : count;

            // 個数は出さず、ツールチップは不足文 → 上書き → 名前の順
            // Hide the count; tooltip order is shortage text, override, then name
            var showsBadge = content.Shortage == null && (owned || ghostShown) && shownCount.HasValue && 0 < shownCount.Value;
            var tooltip = shownIcon == null ? null : content.Shortage?.Tooltip ?? content.TooltipOverride ?? shownIcon.Label;
            return new ItemSlotPresentation(filled, ghostShown, shownIcon, showsBadge, showsBadge ? shownCount.Value : 0, tooltip);
        }
    }
}
