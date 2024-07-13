using System;
using Game.Map.Interface.MapObject;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Map.Interface.Json
{
    [Serializable]
    public class MapObjectJsonObject
    {
        [JsonProperty("instanceId")] public int instanceId;
        [JsonProperty("isDestroyed")] public bool isDestroyed;
        [JsonProperty("hp")] public int hp;
        [JsonProperty("type")] public string type;
        [JsonProperty("x")] public float x;
        [JsonProperty("y")] public float y;
        [JsonProperty("z")] public float z;
        
        [Obsolete("Json用にのみ使用してください。")]
        public MapObjectJsonObject()
        {
        }
        
        public MapObjectJsonObject(IMapObject mapObject)
        {
            instanceId = mapObject.InstanceId;
            isDestroyed = mapObject.IsDestroyed;
            type = mapObject.Type;
            hp = mapObject.CurrentHp;
            x = mapObject.Position.x;
            y = mapObject.Position.y;
            z = mapObject.Position.z;
        }
        
        [JsonIgnore] public Vector3 Position => new(x, y, z);
    }
}