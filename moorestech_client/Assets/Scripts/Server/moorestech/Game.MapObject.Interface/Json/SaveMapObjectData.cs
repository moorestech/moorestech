using System;
using Game.Base;
using Newtonsoft.Json;

namespace Game.MapObject.Interface.Json
{
    [Serializable]
    public class SaveMapObjectData
    {
        [JsonProperty("instanceId")] public int InstanceId;
        [JsonProperty("isDestroyed")] public bool IsDestroyed;
        [JsonProperty("type")] public string Type;
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("z")] public float Z;

        [Obsolete("Json用にのみ使用してください。")]
        public SaveMapObjectData()
        {
        }

        public SaveMapObjectData(IMapObject mapObject)
        {
            InstanceId = mapObject.InstanceId;
            IsDestroyed = mapObject.IsDestroyed;
            Type = mapObject.Type;
            X = mapObject.Position.X;
            Y = mapObject.Position.Y;
            Z = mapObject.Position.Z;
        }

        public ServerVector3 Position => new(X, Y, Z);
    }
}