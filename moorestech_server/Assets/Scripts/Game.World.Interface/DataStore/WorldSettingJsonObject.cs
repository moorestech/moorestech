using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public class WorldSettingJsonObject
    {
        [JsonProperty("SpawnX")] public float SpawnX;
        [JsonProperty("SpawnY")] public float SpawnY;
        [JsonProperty("SpawnZ")] public float SpawnZ;
        

        // プレイ時間関連
        [JsonProperty("WorldCreationDateTime")] public string WorldCreationDateTime;
        [JsonProperty("TotalPlayTimeSeconds")] public double TotalPlayTimeSeconds;
        [JsonProperty("LastSessionStartDateTime")] public string LastSessionStartDateTime;

        public WorldSettingJsonObject(Vector3 spawnPoint, DateTime worldCreationDateTime, TimeSpan totalPlayTimeSeconds, DateTime lastSessionStartDateTime)
        {
            SpawnX = spawnPoint.x;
            SpawnY = spawnPoint.y;
            SpawnZ = spawnPoint.z;
            WorldCreationDateTime = worldCreationDateTime.ToString("o");
            TotalPlayTimeSeconds = totalPlayTimeSeconds.TotalSeconds;
            LastSessionStartDateTime = lastSessionStartDateTime.ToString("o");
        }
        
        [Obsolete("This constructor is for deserialization. Do not use directly.")]
        public WorldSettingJsonObject() { }
    }
}