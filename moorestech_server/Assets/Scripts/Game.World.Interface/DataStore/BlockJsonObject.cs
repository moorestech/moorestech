using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public class BlockJsonObject
    {
        [JsonProperty("blockGuid")] public string BlockGuidStr;
        [JsonIgnore] public Guid BlockGuid => Guid.Parse(BlockGuidStr);
        
        [JsonProperty("direction")] public int Direction;
        [JsonProperty("instanceId")] public int InstanceId;
        [JsonProperty("state")] public Dictionary<string,string> ComponentStates;
        
        [JsonIgnore] public Vector3Int Pos => new(X, Y, Z);
        [JsonProperty("X")] public int X;
        [JsonProperty("Y")] public int Y;
        [JsonProperty("Z")] public int Z;
        
        public BlockJsonObject(Vector3Int pos, string blockGuid, int instanceId, Dictionary<string,string> componentStates, int direction)
        {
            X = pos.x;
            Y = pos.y;
            Z = pos.z;
            BlockGuidStr = blockGuid;
            InstanceId = instanceId;
            ComponentStates = componentStates;
            Direction = direction;
        }
    }
}