using System;
using System.Collections.Generic;
using Core.Master;

namespace Game.Block.Blocks.TrainRail.Naming
{
    // 駅の既定名に使う英語名。modの英語訳が無ければマスタのnameへ縮退する
    // English names for default station names; falls back to the master name when no mod translation exists
    public class TrainStationFeatureEnglishNames
    {
        private readonly IReadOnlyDictionary<string, string> _englishTexts;

        public TrainStationFeatureEnglishNames(IReadOnlyDictionary<string, string> englishTexts)
        {
            _englishTexts = englishTexts;
        }

        public string GetItemName(Guid itemGuid)
        {
            // キーはADR 0006の導出規約 <type>.<guid>.name
            // Keys follow the ADR 0006 convention <type>.<guid>.name
            if (_englishTexts.TryGetValue($"item.{itemGuid}.name", out var english)) return english;
            return MasterHolder.ItemMaster.GetItemMaster(itemGuid).Name;
        }

        public string GetFluidName(Guid fluidGuid)
        {
            if (_englishTexts.TryGetValue($"fluid.{fluidGuid}.name", out var english)) return english;
            return MasterHolder.FluidMaster.GetFluidMaster(fluidGuid).Name;
        }
    }
}
