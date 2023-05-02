using Newtonsoft.Json;

namespace Game.MapObject.Interface
{
    public class SaveMapObjectData
    {
        [JsonProperty("instanceId")] public int InstanceId;
        [JsonProperty("isDestroyed")] public bool IsDestroyed;

        public SaveMapObjectData(int instanceId, bool idIsDestroyed)
        {
            InstanceId = instanceId;
            IsDestroyed = idIsDestroyed;
        }
    }
}