using Newtonsoft.Json;

namespace Game.World.Interface.DataStore
{
    public class WorldSettingSaveData
    {
        [JsonProperty("SpawnX")] public int SpawnX;
        [JsonProperty("SpawnY")] public int SpawnY;

        public WorldSettingSaveData(Coordinate spawnPoint)
        {
            SpawnX = spawnPoint.X;
            SpawnY = spawnPoint.Y;
        }
    }
}