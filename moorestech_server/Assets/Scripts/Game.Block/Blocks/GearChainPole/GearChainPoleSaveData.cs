using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Block.Blocks.GearChainPole
{
    public class GearChainPoleSaveData
    {
        [JsonProperty("targetBlockInstanceIds")]
        public IReadOnlyCollection<int> TargetBlockInstanceIds { get; }
        
        public GearChainPoleSaveData(IReadOnlyCollection<int> targetBlockInstanceIds)
        {
            TargetBlockInstanceIds = targetBlockInstanceIds ?? new List<int>();
        }
        
        public GearChainPoleSaveData(){ }
    }
}