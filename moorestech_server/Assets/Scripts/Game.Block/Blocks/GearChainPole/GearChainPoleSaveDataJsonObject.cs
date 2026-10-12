using System.Collections.Generic;
using Game.Block.Blocks.ConnectionLine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Newtonsoft.Json;

namespace Game.Block.Blocks.GearChainPole
{
    public class GearChainPoleSaveDataJsonObject
    {
        [JsonProperty("connections")]
        public List<ConnectionLineConnectionJsonObject> Connections { get; private set; }

        public GearChainPoleSaveDataJsonObject(IReadOnlyDictionary<BlockInstanceId, (IGearEnergyTransformer Transformer, ConnectionLineRecord Record)> chainTargets)
        {
            // DictionaryからConnectionDataのリストに変換する
            // Convert Dictionary to List of ConnectionData
            Connections = new List<ConnectionLineConnectionJsonObject>();
            foreach (var target in chainTargets)
            {
                Connections.Add(new ConnectionLineConnectionJsonObject(target.Key.AsPrimitive(), target.Value.Record));
            }

            // Dictionaryの列挙順は削除跡の再利用で変わる。添字位置で突き合わせる比較器のため保存側で正準化する
            // Dictionary order shifts as removed slots get reused, so canonicalize here for comparers that match by index
            Connections.Sort((left, right) => left.TargetBlockInstanceId.CompareTo(right.TargetBlockInstanceId));
        }

        public GearChainPoleSaveDataJsonObject()
        {
            Connections = new List<ConnectionLineConnectionJsonObject>();
        }
    }
}
