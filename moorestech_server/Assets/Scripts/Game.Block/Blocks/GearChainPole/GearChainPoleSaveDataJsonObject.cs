using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
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
                Connections.Add(new GearChainPoleConnectionJsonObject(target.Key.AsPrimitive(), target.Value.Cost.Materials));
            }
        }

        public GearChainPoleSaveDataJsonObject() { Connections = new List<GearChainPoleConnectionJsonObject>(); }
    }


    public class GearChainPoleConnectionJsonObject
    {
        [JsonProperty("targetBlockInstanceId")] public int TargetBlockInstanceId { get; set; }
        [JsonProperty("materials")] public List<ConnectToolMaterialSaveJsonObject> Materials { get; set; }

        public GearChainPoleConnectionJsonObject() { Materials = new List<ConnectToolMaterialSaveJsonObject>(); }

        public GearChainPoleConnectionJsonObject(int targetBlockInstanceId, IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            TargetBlockInstanceId = targetBlockInstanceId;
            Materials = materials == null
                ? new List<ConnectToolMaterialSaveJsonObject>()
                : materials.Select(m => new ConnectToolMaterialSaveJsonObject(m)).ToList();
        }

        // ロード時に永続素材からコストを復元する
        // Restore the cost from persisted materials on load
        public GearChainConnectionCost ToConnectionCost()
        {
            var materials = (Materials ?? new List<ConnectToolMaterialSaveJsonObject>())
                .Select(m => m.ToMaterialCost()).ToList();
            return new GearChainConnectionCost(materials);
        }
    }
}
