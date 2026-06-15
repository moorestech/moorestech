using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Core.Master;
using Newtonsoft.Json;

namespace Game.Block.Blocks.GearChainPole
{
    public class GearChainPoleSaveDataJsonObject
    {
        [JsonProperty("connections")]
        public List<GearChainPoleConnectionJsonObject> Connections { get; set; }
        
        public GearChainPoleSaveDataJsonObject(Dictionary<BlockInstanceId, (IGearEnergyTransformer Transformer, GearChainConnectionCost Cost)> chainTargets)
        {
            // DictionaryからConnectionDataのリストに変換する
            // Convert Dictionary to List of ConnectionData
            Connections = new List<GearChainPoleConnectionJsonObject>();
            foreach (var target in chainTargets)
            {
                var cost = target.Value.Cost;
                Connections.Add(new GearChainPoleConnectionJsonObject(target.Key.AsPrimitive(), MasterHolder.ItemMaster.GetItemGuid(cost.ItemId), cost.Count));
            }
        }
        
        public GearChainPoleSaveDataJsonObject() { Connections = new List<GearChainPoleConnectionJsonObject>(); }
    }
    
    
    public class GearChainPoleConnectionJsonObject
    {
        [JsonProperty("targetBlockInstanceId")] public int TargetBlockInstanceId { get; set; }
        [JsonProperty("itemGuid")] public string ItemGuidStr { get; set; }
        [JsonProperty("count")] public int Count { get; set; }

        [JsonIgnore] public Guid ItemGuid => Guid.Parse(ItemGuidStr);

        public GearChainPoleConnectionJsonObject() { }

        public GearChainPoleConnectionJsonObject(int targetBlockInstanceId, Guid itemGuid, int count)
        {
            TargetBlockInstanceId = targetBlockInstanceId;
            ItemGuidStr = itemGuid.ToString();
            Count = count;
        }
    }
}
