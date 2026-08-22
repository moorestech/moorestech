using Client.Game.InGame.UI.Tooltip;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 不足素材1件を「名前 所持/必要」のツールチップ行へ写像する
    /// Maps one short construction material to a "name held/required" tooltip line
    /// </summary>
    public static class ConstructionMaterialShortageLine
    {
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
