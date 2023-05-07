using Game.Base;
using Newtonsoft.Json;

namespace Game.MapObject.Interface.Json
{
    public class ConfigMapObjects
    {
        [JsonProperty("mapObjects")] public ConfigMapObjectData[] MapObjects;
    }
    
    public class ConfigMapObjectData
    {
        [JsonProperty("type")]public string Type;
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("z")] public float Z;
        
        public ServerVector3 Position => new(X,Y,Z);
    }
}