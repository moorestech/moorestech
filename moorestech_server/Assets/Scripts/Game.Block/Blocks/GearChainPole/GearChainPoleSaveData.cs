using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Block.Blocks.GearChainPole
{
    public class GearChainPoleSaveData
    {
        [JsonProperty("targetBlockInstanceIds")]
        public IReadOnlyCollection<int> TargetBlockInstanceIds { get; }
        
        [JsonProperty("connections")]
        public List<ConnectionData> Connections { get; }
        
        public GearChainPoleSaveData(IReadOnlyCollection<int> targetBlockInstanceIds)
        {
            TargetBlockInstanceIds = targetBlockInstanceIds ?? new List<int>();
            Connections = new List<ConnectionData>();
        }

        public GearChainPoleSaveData(List<ConnectionData> connections)
        {
            Connections = connections ?? new List<ConnectionData>();
            TargetBlockInstanceIds = new List<int>();
        }
        
        public GearChainPoleSaveData(){ }

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
