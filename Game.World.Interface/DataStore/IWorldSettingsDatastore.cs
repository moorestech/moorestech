namespace Game.World.Interface.DataStore
{
    public interface IWorldSettingsDatastore
    {
        public Coordinate WorldSpawnPoint { get; }
        
        public void Initialize();
        public WorldSettingSaveData GetSettingsSaveData();
        public void LoadSettingData(WorldSettingSaveData worldSettingSaveData);
    }
}