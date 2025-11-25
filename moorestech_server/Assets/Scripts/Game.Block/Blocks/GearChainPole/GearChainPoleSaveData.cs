using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Core.Master;
using Newtonsoft.Json;

namespace Game.Block.Blocks.GearChainPole
{
    public class GearChainPoleSaveData
    {
        [JsonProperty("connections")]
        public List<ConnectionData> Connections { get; set; }
        
        public GearChainPoleSaveData(Dictionary<BlockInstanceId, (IGearEnergyTransformer Transformer, GearChainConnectionCost Cost)> chainTargets)
        {
            // DictionaryからConnectionDataのリストに変換する
            // Convert Dictionary to List of ConnectionData
            Connections = new List<ConnectionData>();
            foreach (var target in chainTargets)
            {
                var cost = target.Value.Cost;
                Connections.Add(new ConnectionData(target.Key.AsPrimitive(), cost.ItemId.AsPrimitive(), cost.Count));
            }
        }
        
        public GearChainPoleSaveData() { Connections = new List<ConnectionData>(); }

        public class ConnectionData
        {
            public ConnectionData(int targetBlockInstanceId, int itemId, int count)
            {
                TargetBlockInstanceId = targetBlockInstanceId;
                ItemId = itemId;
                Count = count;
            }

            [JsonProperty("targetBlockInstanceId")] public int TargetBlockInstanceId { get; }
            [JsonProperty("itemId")] public int ItemId { get; }
            [JsonProperty("count")] public int Count { get; }
        }
    }
}
