using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Core.Master;
using Game.EnergySystem;
using Newtonsoft.Json;

namespace Game.Block.Blocks.ElectricWire
{
    public class ElectricWireSaveDataJsonObject
    {
        [JsonProperty("connections")]
        public List<ElectricWireConnectionJsonObject> Connections { get; set; }

        public ElectricWireSaveDataJsonObject(Dictionary<BlockInstanceId, (IElectricWireConnector Connector, ElectricWireConnectionCost Cost)> wireConnections)
        {
            // 接続をリストに変換する
            // Convert Dictionary to List of ConnectionData
            Connections = new List<ElectricWireConnectionJsonObject>();
            foreach (var target in wireConnections)
            {
                var cost = target.Value.Cost;
                Connections.Add(new ElectricWireConnectionJsonObject(target.Key.AsPrimitive(), MasterHolder.ItemMaster.GetItemGuid(cost.ItemId), cost.Count));
            }
        }

        public ElectricWireSaveDataJsonObject() { Connections = new List<ElectricWireConnectionJsonObject>(); }
    }


    public class ElectricWireConnectionJsonObject
    {
        [JsonProperty("targetBlockInstanceId")] public int TargetBlockInstanceId { get; set; }
        [JsonProperty("itemGuid")] public string ItemGuidStr { get; set; }
        [JsonProperty("count")] public int Count { get; set; }

        [JsonIgnore] public Guid ItemGuid => Guid.Parse(ItemGuidStr);

        public ElectricWireConnectionJsonObject() { }

        public ElectricWireConnectionJsonObject(int targetBlockInstanceId, Guid itemGuid, int count)
        {
            TargetBlockInstanceId = targetBlockInstanceId;
            ItemGuidStr = itemGuid.ToString();
            Count = count;
        }
    }
}
