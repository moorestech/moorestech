using Game.World.Interface.DataStore;
using Game.WorldMap;

namespace World.DataStore.WorldSettings
{
    /// <summary>
    ///     
    ///     TODO 
    /// </summary>
    public class WorldSettingsDatastore : IWorldSettingsDatastore
    {
        private readonly VeinGenerator _veinGenerator;

        public WorldSettingsDatastore(VeinGenerator veinGenerator)
        {
            _veinGenerator = veinGenerator;
        }

        public Coordinate WorldSpawnPoint { get; private set; }

        public void Initialize()
        {
            WorldSpawnPoint = WorldSpawnPointSearcher.SearchSpawnPoint(_veinGenerator);
        }

        public WorldSettingSaveData GetSettingsSaveData()
        {
            return new WorldSettingSaveData(WorldSpawnPoint);
        }

        public void LoadSettingData(WorldSettingSaveData worldSettingSaveData)
        {
            WorldSpawnPoint = new Coordinate(worldSettingSaveData.SpawnX, worldSettingSaveData.SpawnY);
        }
    }
}