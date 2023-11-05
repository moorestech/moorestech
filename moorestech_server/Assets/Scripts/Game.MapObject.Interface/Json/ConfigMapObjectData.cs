using Newtonsoft.Json;
using UnityEngine;

namespace Game.MapObject.Interface.Json
{
    public class ConfigMapObjects
    {
        [JsonProperty("mapObjects")] public ConfigMapObjectData[] MapObjects;
    }

    public class ConfigMapObjectData
    {
        [JsonProperty("instanceId")] public int InstanceId;
        [JsonProperty("type")] public string Type;
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("z")] public float Z;

        public Vector3 Position => new(X, Y, Z);
    }
}