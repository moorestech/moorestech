using Newtonsoft.Json;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public class WorldSettingSaveData
    {
        [JsonProperty("SpawnX")] public int SpawnX;
        [JsonProperty("SpawnY")] public int SpawnY;

        public WorldSettingSaveData(Vector2Int spawnPoint)
        {
            SpawnX = spawnPoint.x;
            SpawnY = spawnPoint.y;
        }
    }
}