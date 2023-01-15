using Game.World.Interface.DataStore;

namespace World.DataStore.WorldSettings
{
    public class WorldSettingsDatastore : IWorldSettingsDatastore
    {
        public WorldSettingsDatastore()
        {
        }

        public Coordinate WorldSpawnPoint { get; }
    }
}