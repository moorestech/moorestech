using Newtonsoft.Json;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public class WorldSettingJsonObject
    {
        [JsonProperty("SpawnX")] public int SpawnX;
        [JsonProperty("SpawnY")] public int SpawnY;
        
        public WorldSettingJsonObject(Vector3Int spawnPoint)
        {
            SpawnX = spawnPoint.x;
            SpawnY = spawnPoint.y;
        }
    }
}