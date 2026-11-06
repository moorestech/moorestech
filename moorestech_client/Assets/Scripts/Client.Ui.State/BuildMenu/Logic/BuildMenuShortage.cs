using System.Collections.Generic;
using System.Linq;

namespace Client.Ui.State.BuildMenu.Logic
{
    /// <summary>
    /// 表示上の不足の合成（Web buildMenuShortage.ts の写し）。合成はここが唯一の場所
    /// Composes the displayed shortage (port of the Web buildMenuShortage.ts); this is the only place it composes
    /// </summary>
    public static class BuildMenuShortage
    {
        // 表示上の不足は「素材が足りない」かつ「支払いが免除されていない」
        // A displayed shortage means the material is short and the payment is not waived
        public static bool IsItemInsufficient(BuildMenuCatalogEntry entry, BuildMenuRequiredItem item)
        {
            return !entry.PaymentWaived && item.Lacking;
        }

        // ツールチップに出す不足の行。免除中は 1 件も出さない
        // The shortage rows for the tooltip; a waived entry yields none
        public static IReadOnlyList<BuildMenuRequiredItem> InsufficientItems(BuildMenuCatalogEntry entry)
        {
            return entry.RequiredItems.Where(item => IsItemInsufficient(entry, item)).ToList();
        }
    }
}
