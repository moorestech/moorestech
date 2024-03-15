using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Map.Interface.Json
{
    [Serializable]
    public class SaveMapObjectData
    {
        [JsonProperty("instanceId")] public int instanceId;
        [JsonProperty("isDestroyed")] public bool isDestroyed;
        [JsonProperty("type")] public string type;
        [JsonProperty("x")] public float x;
        [JsonProperty("y")] public float y;
        [JsonProperty("z")] public float z;

        [Obsolete("Json用にのみ使用してください。")]
        public SaveMapObjectData()
        {
        }

        public SaveMapObjectData(IMapObject mapObject)
        {
            instanceId = mapObject.InstanceId;
            isDestroyed = mapObject.IsDestroyed;
            type = mapObject.Type;
            x = mapObject.Position.x;
            y = mapObject.Position.y;
            z = mapObject.Position.z;
        }

        [JsonIgnore]
        public Vector3 Position => new(x,y, z);
    }
}