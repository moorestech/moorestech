using System.Collections.Generic;
using Client.Game.InGame.UI.Tooltip;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 不足素材1件をアイテム名・所持・必要のパラメータへ写像する
    /// Maps one short material to a line with item name, held and required params
    /// 表示文言（接頭辞や並び）の正本はlocalization.csv側にある
    /// The wording itself (prefix and ordering) is owned by localization.csv
    /// </summary>
    public static class ConstructionMaterialShortageLine
    {
        /// <summary>
        /// 不足素材の行群を作る。不足が1件も無いとき（接続ツールのマスタ欠損など）は無言にせず汎用の不可文言1行へ落とす
        /// Builds the shortage lines; with no shortage at all (e.g. a missing connect tool master) it falls back to one generic line instead of staying silent
        /// </summary>
        public static IReadOnlyList<TooltipLine> ToLines(IReadOnlyList<ConstructionMaterialShortage> shortages, LocalizationKey emptyFallbackKey)
        {
            if (shortages.Count == 0) return new[] { new TooltipLine(emptyFallbackKey) };

            var lines = new List<TooltipLine>(shortages.Count);
            foreach (var shortage in shortages) lines.Add(ToLine(shortage));
            return lines;
        }

        // アイテム名は表示言語で解決してからパラメータへ渡す（前例: MiningFocusStateの必要道具名）
        // The item name is resolved in the display language before it becomes a param (precedent: MiningFocusState's required tools)
        public static TooltipLine ToLine(ConstructionMaterialShortage shortage)
        {
            var itemGuid = MasterHolder.ItemMaster.GetItemMaster(shortage.ItemId).ItemGuid;
            var itemName = Localize.GetContent(ContentLocalizationKeys.ItemName(itemGuid));
            return new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage, new[] { itemName, shortage.Held.ToString(), shortage.Required.ToString() });
        }
    }
}
