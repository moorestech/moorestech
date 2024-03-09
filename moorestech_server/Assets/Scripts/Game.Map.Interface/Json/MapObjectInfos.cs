using Newtonsoft.Json;
using UnityEngine;

namespace Game.MapObject.Interface.Json
{
    public class MapInfo
    {
        [JsonProperty("mapObjects")] public MapObjectInfos[] MapObjects;
        
    }

    public class MapObjectInfos
    {
        [JsonProperty("instanceId")] public int InstanceId;
        [JsonProperty("type")] public string Type;
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("z")] public float Z;

        [JsonIgnore]
        public Vector3 Position => new(X,Y, Z);
    }
}