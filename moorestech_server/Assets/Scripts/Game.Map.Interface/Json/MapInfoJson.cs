using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Map.Interface.Json
{
    public class MapInfoJson
    {
        [JsonProperty("defaultSpawnPoint")] public SpawnPointJson DefaultSpawnPointJson;
        [JsonProperty("mapObjects")] public List<MapObjectInfoJson> MapObjects;
        [JsonProperty("itemMapVeins")] public List<ItemMapVeinInfoJson> ItemMapVeins;
        [JsonProperty("fluidVeins")] public List<FluidVeinInfoJson> FluidVeins;
    }
    
    public class MapObjectInfoJson
    {
        [JsonProperty("instanceId")] public int InstanceId;
        [JsonProperty("mapObjectGuid")] public string MapObjectGuidStr;
        [JsonIgnore] public Guid MapObjectGuid => new(MapObjectGuidStr);
        
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("z")] public float Z;
        
        [JsonIgnore] public Vector3 Position => new(X, Y, Z);
    }
    
    public class ItemMapVeinInfoJson
    {
        [JsonProperty("veinItemGuid")] public string VeinItemGuidStr;
        [JsonIgnore] public Guid VeinItemGuid => Guid.Parse(VeinItemGuidStr);
        
        [JsonIgnore] public Vector3Int MinPosition => new(MinX, MinY, MinZ);
        [JsonProperty("minX")] public int MinX;
        [JsonProperty("minY")] public int MinY;
        [JsonProperty("minZ")] public int MinZ;
        
        [JsonIgnore] public Vector3Int MaxPosition => new(MaxX, MaxY, MaxZ);
        [JsonProperty("maxX")] public int MaxX;
        [JsonProperty("maxY")] public int MaxY;
        [JsonProperty("maxZ")] public int MaxZ;
    }
    
    public class FluidVeinInfoJson
    {
        [JsonProperty("veinFluidGuid")] public string VeinFluidGuidStr;
        [JsonIgnore] public Guid VeinFluidGuid => Guid.Parse(VeinFluidGuidStr);

        [JsonIgnore] public Vector3Int MinPosition => new(MinX, MinY, MinZ);
        [JsonProperty("minX")] public int MinX;
        [JsonProperty("minY")] public int MinY;
        [JsonProperty("minZ")] public int MinZ;

        [JsonIgnore] public Vector3Int MaxPosition => new(MaxX, MaxY, MaxZ);
        [JsonProperty("maxX")] public int MaxX;
        [JsonProperty("maxY")] public int MaxY;
        [JsonProperty("maxZ")] public int MaxZ;
    }

    public class SpawnPointJson
    {
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("z")] public float Z;
        
        [JsonIgnore] public Vector3 Position => new(X, Y, Z);
    }
}