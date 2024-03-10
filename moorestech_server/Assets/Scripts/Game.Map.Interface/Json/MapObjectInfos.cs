using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Map.Interface.Json
{
    public class MapInfo
    {
        [JsonProperty("mapObjects")] public List<MapObjectInfos> MapObjects;
        [JsonProperty("mapVeins")] public List<MapVeinInfo> MapVeins;
    }

    public class MapObjectInfos
    {
        [JsonProperty("instanceId")] public int InstanceId;
        [JsonProperty("type")] public string Type;
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("z")] public float Z;

        [JsonIgnore]
        public Vector3 Position => new(X,Y, Z);
    }

    public class MapVeinInfo
    {
        [JsonProperty("veinItemModId")] public string ItemModId;
        [JsonProperty("veinItemId")] public string ItemId;
        
        [JsonProperty("xMin")] public int XMin;
        [JsonProperty("yMin")] public int YMin;
        
        [JsonProperty("xMax")] public int XMax;
        [JsonProperty("yMax")] public int YMax;
    }
}