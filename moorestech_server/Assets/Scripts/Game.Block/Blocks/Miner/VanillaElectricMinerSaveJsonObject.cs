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

        // 復元時に採掘対象が変わっていないかを見るための対象アイテム
        // The target items, used on load to see whether the mining targets changed
        [JsonProperty("miningItemGuids")]
        public List<string> MiningItemGuids;
    }
}
