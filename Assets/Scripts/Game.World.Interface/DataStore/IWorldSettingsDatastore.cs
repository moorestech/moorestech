using Core.Util;

namespace Game.World.Interface.DataStore
{
    public interface IWorldSettingsDatastore
    {
        public CoreVector2Int WorldSpawnPoint { get; }

        public WorldSettingSaveData GetSettingsSaveData();
        public void Initialize();
        public void LoadSettingData(WorldSettingSaveData worldSettingSaveData);
    }
}