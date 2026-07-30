using System.Collections.Generic;
using Core.Master;

namespace Client.Localization
{
    public static class MasterSourceTextCollector
    {
        public static Dictionary<string, string> Collect()
        {
            var sourceTexts = new Dictionary<string, string>();

            // アイテムの安定Guidから原文フォールバックを構築する
            // Build source fallbacks from stable item GUIDs
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(itemId);
                sourceTexts[ContentLocalizationKeys.ItemName(itemMaster.ItemGuid)] = itemMaster.Name;
            }

            // ブロックも同じ導出規約で原文を収集する
            // Collect block sources with the same derived-key convention
            foreach (var blockMaster in MasterHolder.BlockMaster.Blocks.Data)
            {
                sourceTexts[ContentLocalizationKeys.BlockName(blockMaster.BlockGuid)] = blockMaster.Name;
            }

            return sourceTexts;
        }
    }
}
