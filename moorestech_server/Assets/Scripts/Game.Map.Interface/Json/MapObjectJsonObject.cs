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
        [JsonProperty("guid")] public string guidStr;
        [JsonIgnore] public Guid MapObjectGuid => new(guidStr);
        
        [JsonProperty("x")] public float x;
        [JsonProperty("y")] public float y;
        [JsonProperty("z")] public float z;
        
        [Obsolete("This is for JSON only. Do not use directly.")]
        public MapObjectJsonObject()
        {
        }
        
        public MapObjectJsonObject(IMapObject mapObject)
        {
            instanceId = mapObject.InstanceId;
            isDestroyed = mapObject.IsDestroyed;
            guidStr = mapObject.MapObjectGuid.ToString();
            hp = mapObject.CurrentHp;
            x = mapObject.Position.x;
            y = mapObject.Position.y;
            z = mapObject.Position.z;
        }
        
        [JsonIgnore] public Vector3 Position => new(x, y, z);
    }
}