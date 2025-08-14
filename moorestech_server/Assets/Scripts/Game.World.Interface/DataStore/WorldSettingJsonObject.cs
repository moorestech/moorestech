using Newtonsoft.Json;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public class WorldSettingJsonObject
    {
        [JsonProperty("SpawnX")] public float SpawnX;
        [JsonProperty("SpawnY")] public float SpawnY;
        [JsonProperty("SpawnZ")] public float SpawnZ;
        
        public WorldSettingJsonObject(Vector3 spawnPoint)
        {
            SpawnX = spawnPoint.x;
            SpawnY = spawnPoint.y;
            SpawnZ = spawnPoint.z;
        }
    }
}