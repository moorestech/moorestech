using System;
using System.Collections.Generic;
using Core.Master;

namespace Game.Block.Blocks.TrainRail.Naming
{
    // 駅名用のmod英語訳
    // Mod English translations for station names
    // 未翻訳ならマスタ名
    // Use the master name when untranslated
    public class TrainStationFeatureEnglishNames
    {
        private readonly IReadOnlyDictionary<string, string> _englishTexts;

        public TrainStationFeatureEnglishNames(IReadOnlyDictionary<string, string> englishTexts)
        {
            _englishTexts = englishTexts;
        }

        internal string GetItemName(Guid itemGuid)
        {
            // キーはADR 0006の導出規約 <type>.<guid>.name
            // Keys follow the ADR 0006 convention <type>.<guid>.name
            if (_englishTexts.TryGetValue($"item.{itemGuid}.name", out var english)) return english;
            return MasterHolder.ItemMaster.GetItemMaster(itemGuid).Name;
        }

        internal string GetFluidName(Guid fluidGuid)
        {
            if (_englishTexts.TryGetValue($"fluid.{fluidGuid}.name", out var english)) return english;
            return MasterHolder.FluidMaster.GetFluidMaster(fluidGuid).Name;
        }
    }
}
