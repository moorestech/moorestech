﻿using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Map.Interface.Json
{
    public class MapInfoJson
    {
        [JsonProperty("mapObjects")] public List<MapObjectInfoJson> MapObjects;
        [JsonProperty("mapVeins")] public List<MapVeinInfoJson> MapVeins;
    }

    public class MapObjectInfoJson
    {
        [JsonProperty("instanceId")] public int InstanceId;
        [JsonProperty("type")] public string Type;

        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("z")] public float Z;

        [JsonIgnore]
        public Vector3 Position => new(X, Y, Z);
    }

    public class MapVeinInfoJson
    {
        [JsonProperty("veinItemName")] public string ItemId;
        [JsonProperty("veinItemModId")] public string ItemModId;

        [JsonProperty("xMax")] public int XMax;

        [JsonProperty("xMin")] public int XMin;
        [JsonProperty("yMax")] public int YMax;
        [JsonProperty("yMin")] public int YMin;
    }
}