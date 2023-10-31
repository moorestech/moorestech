using Game.Base;
using Newtonsoft.Json;

namespace Game.Entity.Interface
{
    public class SaveEntityData
    {
        [JsonProperty("InstanceId")] public long InstanceId;
        [JsonProperty("Type")] public string Type;

        [JsonProperty("X")] public float X;
        [JsonProperty("Y")] public float Y;
        [JsonProperty("Z")] public float Z;

        public SaveEntityData(string type, long instanceId, ServerVector3 serverVector3)
        {
            Type = type;
            InstanceId = instanceId;
            X = serverVector3.X;
            Y = serverVector3.Y;
            Z = serverVector3.Z;
        }
    }
}