using System.Collections.Generic;
using Core.Master;

namespace Core.Item
{
    /// <summary>
    ///     上位レベルの品を下位の要求に充てる照合規則。機械の入力束縛・研究の充足と消費・クライアントの表示が共有する
    ///     The rule that lets a higher-level item serve a lower requirement, shared by machine input binding, research sufficiency and consumption, and the client display
    /// </summary>
    public static class LevelFamilySubstituteUtil
    {
        // 要求に充てられるアイテム（下位から順）。要求がファミリーの基準なら全変種、それ以外は要求そのものだけ
        // Items that can serve the requirement, lowest first; every variant when it is a family base, otherwise just the requirement itself
        public static IReadOnlyList<ItemId> GetAcceptedItemIds(ItemId requiredItemId)
        {
            return MasterHolder.ItemMaster.GetLevelVariants(requiredItemId);
        }

        // 充てられるならそのレベル（1始まり）、充てられなければ null
        // The 1-based level when the item can serve the requirement, otherwise null
        public static int? ResolveAcceptedLevel(ItemId requiredItemId, ItemId itemId)
        {
            // 単独品と特定変種は列挙せず厳密一致する
            // Match standalone items and specific variants without allocating an array
            if (!MasterHolder.ItemMaster.HasLevelFamily(requiredItemId)) return requiredItemId == itemId ? 1 : null;

            var accepted = GetAcceptedItemIds(requiredItemId);
            for (var i = 0; i < accepted.Count; i++)
            {
                if (accepted[i] == itemId) return i + 1;
            }
            return null;
        }
    }
}
