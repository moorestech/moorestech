using Newtonsoft.Json;

namespace Game.MapObject.Interface
{
    public class SaveMapObjectData
    {
        [JsonProperty("id")] public int Id;
        [JsonProperty("isDestroyed")] public bool IsDestroyed;

        public SaveMapObjectData(int id, bool idIsDestroyed)
        {
            Id = id;
            IsDestroyed = idIsDestroyed;
        }
    }
}