using Newtonsoft.Json;
using UnityEngine;

namespace Game.Entity.Interface
{
    public class EntityJsonObject
    {
        [JsonProperty("InstanceId")] public long InstanceId;
        [JsonProperty("Type")] public string Type;

        [JsonProperty("X")] public float X;
        [JsonProperty("Y")] public float Y;
        [JsonProperty("Z")] public float Z;

        public EntityJsonObject(string type, long instanceId, Vector3 serverVector3)
        {
            Type = type;
            InstanceId = instanceId;
            X = serverVector3.x;
            Y = serverVector3.y;
            Z = serverVector3.z;
        }
    }
}