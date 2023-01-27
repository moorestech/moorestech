namespace Game.World.Interface.DataStore
{
    public interface IWorldSettingsDatastore
    {
        public Coordinate WorldSpawnPoint { get; }
        
        public void Initialize();
        public WorldSettingSaveData Save();
        public void Load(WorldSettingSaveData worldSettingSaveData);
    }
}