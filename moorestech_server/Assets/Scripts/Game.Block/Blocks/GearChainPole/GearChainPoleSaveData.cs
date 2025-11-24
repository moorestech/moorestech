using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Block.Blocks.GearChainPole
{
    public class GearChainPoleSaveData
    {
        [JsonProperty("connections")]
        public List<ConnectionData> Connections { get; }
        
        public GearChainPoleSaveData(List<ConnectionData> connections)
        {
            Connections = connections ?? new List<ConnectionData>();
        }
        
        public GearChainPoleSaveData()
        {
            Connections = new List<ConnectionData>();
        }

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
