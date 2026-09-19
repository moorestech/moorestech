using System.Collections.Generic;
using Core.Item.Interface;
using Newtonsoft.Json;

namespace Game.Block.Blocks.Miner
{
    public class VanillaElectricMinerSaveJsonObject
    {
        [JsonProperty("items")]
        public List<ItemStackSaveJsonObject> Items;

        // 秒数として保存（tick数の変動に対応）
        // Save as seconds (to handle tick rate changes)
        [JsonProperty("remainingSeconds")]
        public double RemainingSeconds;

        // 採掘対象の変更検知用アイテム
        // Items used to detect mining-target changes
        [JsonProperty("miningItemGuids")]
        public List<string> MiningItemGuids;
    }
}
