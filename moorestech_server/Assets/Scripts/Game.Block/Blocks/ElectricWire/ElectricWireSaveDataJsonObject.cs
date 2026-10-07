using System.Collections.Generic;
using Game.Block.Blocks.ConnectionLine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Newtonsoft.Json;

namespace Game.Block.Blocks.ElectricWire
{
    public class ElectricWireSaveDataJsonObject
    {
        [JsonProperty("connections")]
        public List<ConnectionLineConnectionJsonObject> Connections { get; private set; }

        public ElectricWireSaveDataJsonObject(Dictionary<BlockInstanceId, (IElectricWireConnector Connector, ConnectionLineRecord Record)> wireConnections)
        {
            // 接続をリストに変換する
            // Convert Dictionary to List of ConnectionData
            Connections = new List<ConnectionLineConnectionJsonObject>();
            foreach (var target in wireConnections)
            {
                Connections.Add(new ConnectionLineConnectionJsonObject(target.Key.AsPrimitive(), target.Value.Record));
            }

            // Dictionaryの列挙順は削除跡の再利用で変わる。添字位置で突き合わせる比較器のため保存側で正準化する
            // Dictionary order shifts as removed slots get reused, so canonicalize here for comparers that match by index
            Connections.Sort((left, right) => left.TargetBlockInstanceId.CompareTo(right.TargetBlockInstanceId));
        }

        public ElectricWireSaveDataJsonObject()
        {
            Connections = new List<ConnectionLineConnectionJsonObject>();
        }
    }
}
