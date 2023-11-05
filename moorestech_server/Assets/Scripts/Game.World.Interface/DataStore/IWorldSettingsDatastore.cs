using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public interface IWorldSettingsDatastore
    {
        public Vector2Int WorldSpawnPoint { get; }

        public WorldSettingSaveData GetSettingsSaveData();
        public void Initialize();
        public void LoadSettingData(WorldSettingSaveData worldSettingSaveData);
    }
}