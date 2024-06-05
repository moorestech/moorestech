using Game.Block.Interface;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public class BlockJsonObject
    {
        [JsonProperty("blockHash")] public long BlockHash;
        [JsonProperty("direction")] public int Direction;
        [JsonProperty("entityId")] public EntityID EntityId;
        [JsonProperty("state")] public string State;
        
        [JsonProperty("X")] public int X;
        [JsonProperty("Y")] public int Y;
        [JsonProperty("Z")] public int Z;
        
        public BlockJsonObject(Vector3Int pos, long blocHash, EntityID entityId, string state, int direction)
        {
            X = pos.x;
            Y = pos.y;
            Z = pos.z;
            BlockHash = blocHash;
            EntityId = entityId;
            State = state;
            Direction = direction;
        }
        
        [JsonIgnore] public Vector3Int Pos => new(X, Y, Z);
    }
}