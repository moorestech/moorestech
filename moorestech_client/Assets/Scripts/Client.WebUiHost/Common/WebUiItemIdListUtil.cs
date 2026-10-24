using System.Collections.Generic;
using Core.Master;

namespace Client.WebUiHost.Common
{
    /// <summary>
    ///     アイテムIDの列を wire の int 配列へ詰め替える（順序は保つ）
    ///     Converts a list of item IDs into the wire's int array, keeping the order
    /// </summary>
    public static class WebUiItemIdListUtil
    {
        public static List<int> ToPrimitiveIds(IReadOnlyList<ItemId> itemIds)
        {
            var ids = new List<int>(itemIds.Count);
            foreach (var itemId in itemIds) ids.Add(itemId.AsPrimitive());
            return ids;
        }
    }
}
