using Client.Ui.Foundation.Icons;
using Client.Ui.State.Text;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// ItemSlotContent から面・中身・バッジ・ツールチップを決める（Web の ItemSlot 本体の式）
    /// Derives face, content, badge and tooltip from ItemSlotContent (the Web ItemSlot body formulas)
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
            // カタログは常に絵を出し、白面は所持数がある時だけ（Web の owned・hasItem・filled）
            // A catalog cell always shows the icon and turns white only when owned (the Web owned, hasItem and filled)
            var count = content.Count;
            var owned = count.HasValue && 0 < count.Value;
            var hasItem = content.Icon != null && (content.Marks.Catalog || !count.HasValue || 0 < count.Value);
            var filled = content.Marks.Catalog ? owned : hasItem;

            // 実物の無い空きマスに限りゴーストを描く。白面は実物だけで決める
            // The ghost draws only on a genuinely empty cell; the white face is decided by the real item alone
            var ghostShown = content.Ghost != null && !hasItem;
            var shownIcon = ghostShown ? content.Ghost.Icon : hasItem ? content.Icon : null;
            var shownCount = ghostShown ? content.Ghost.Count : count;

            // 所持/必要がある間は個数を出さず、ツールチップは不足の文 → 上書き → 表示中の名前の順
            // While owned/required shows the count stays hidden; the tooltip prefers the shortage text, then the override, then the shown name
            var showsBadge = content.Shortage == null && (owned || ghostShown) && shownCount.HasValue && 0 < shownCount.Value;
            var tooltip = shownIcon == null ? null : content.Shortage?.Tooltip ?? content.TooltipOverride ?? shownIcon.Label;
            return new ItemSlotPresentation(filled, ghostShown, shownIcon, showsBadge, showsBadge ? shownCount.Value : 0, tooltip);
        }
    }
}
