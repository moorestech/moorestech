using Newtonsoft.Json;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public class SaveBlockData
    {
        [JsonProperty("blockHash")] public long BlockHash;
        [JsonProperty("direction")] public int Direction;
        [JsonProperty("entityId")] public int EntityId;
        [JsonProperty("state")] public string State;

        [JsonProperty("X")] public int X;
        [JsonProperty("Y")] public int Y;
        
        [JsonIgnore]
        public Vector3Int Pos => new(X,Y);

        public SaveBlockData(Vector3Int pos, long blocHash, int entityId, string state, int direction)
        {
            X = pos.x;
            Y = pos.y;
            BlockHash = blocHash;
            EntityId = entityId;
            State = state;
            Direction = direction;
        }
    }
}