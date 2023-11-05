using System;
using Game.Base;
using Newtonsoft.Json;

namespace Game.MapObject.Interface.Json
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
            x = mapObject.Position.X;
            y = mapObject.Position.Y;
            z = mapObject.Position.Z;
        }

        public ServerVector3 Position => new(x, y, z);
    }
}