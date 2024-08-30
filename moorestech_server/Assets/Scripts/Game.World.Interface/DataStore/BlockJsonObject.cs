using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public class BlockJsonObject
    {
        [JsonProperty("blockGuid")] public string BlockGuidStr;
        [JsonIgnore] public Guid BlockGuid => Guid.Parse(BlockGuidStr);
        
        [JsonProperty("direction")] public int Direction;
        [JsonProperty("entityId")] public int EntityId;
        [JsonProperty("state")] public string State;
        
        [JsonIgnore] public Vector3Int Pos => new(X, Y, Z);
        [JsonProperty("X")] public int X;
        [JsonProperty("Y")] public int Y;
        [JsonProperty("Z")] public int Z;
        
        public BlockJsonObject(Vector3Int pos, string blockGuid, int entityId, string state, int direction)
        {
            X = pos.x;
            Y = pos.y;
            Z = pos.z;
            BlockGuidStr = blockGuid;
            EntityId = entityId;
            State = state;
            Direction = direction;
        }
    }
}