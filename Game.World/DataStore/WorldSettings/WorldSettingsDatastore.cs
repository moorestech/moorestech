using Game.World.Interface.DataStore;
using Game.WorldMap;

namespace World.DataStore.WorldSettings
{
    public class WorldSettingsDatastore : IWorldSettingsDatastore
    {
        public WorldSettingsDatastore(VeinGenerator veinGenerator)
        {
            WorldSpawnPoint = WorldSpawnPointSearcher.SearchSpawnPoint(veinGenerator);
        }

        public Coordinate WorldSpawnPoint { get; }
    }
}