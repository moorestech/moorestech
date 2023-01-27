using Newtonsoft.Json;

namespace Game.World.Interface.DataStore
{
    public class WorldSettingSaveData
    {
        public WorldSettingSaveData(Coordinate spawnPoint)
        {
            SpawnX = spawnPoint.X;
            SpawnY = spawnPoint.Y;
        }


        [JsonProperty("SpawnX")] public int SpawnX;
        [JsonProperty("SpawnY")] public int SpawnY;
    }
}