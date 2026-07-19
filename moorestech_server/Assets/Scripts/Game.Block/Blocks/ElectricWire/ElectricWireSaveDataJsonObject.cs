using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
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
                Connections.Add(new ElectricWireConnectionJsonObject(target.Key.AsPrimitive(), target.Value.Cost.Materials));
            }
        }

        public ElectricWireSaveDataJsonObject() { Connections = new List<ElectricWireConnectionJsonObject>(); }
    }


    public class ElectricWireConnectionJsonObject
    {
        [JsonProperty("targetBlockInstanceId")] public int TargetBlockInstanceId { get; set; }
        [JsonProperty("materials")] public List<ConnectToolMaterialSaveJsonObject> Materials { get; set; }

        public ElectricWireConnectionJsonObject() { Materials = new List<ConnectToolMaterialSaveJsonObject>(); }

        public ElectricWireConnectionJsonObject(int targetBlockInstanceId, IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            TargetBlockInstanceId = targetBlockInstanceId;
            Materials = materials == null
                ? new List<ConnectToolMaterialSaveJsonObject>()
                : materials.Select(m => new ConnectToolMaterialSaveJsonObject(m)).ToList();
        }

        // ロード時に永続素材からコストを復元する
        // Restore the cost from persisted materials on load
        public ElectricWireConnectionCost ToConnectionCost()
        {
            var materials = (Materials ?? new List<ConnectToolMaterialSaveJsonObject>())
                .Select(m => m.ToMaterialCost()).ToList();
            return new ElectricWireConnectionCost(materials);
        }
    }
}
