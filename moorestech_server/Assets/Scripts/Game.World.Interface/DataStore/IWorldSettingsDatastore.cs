using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public interface IWorldSettingsDatastore
    {
        public Vector3Int WorldSpawnPoint { get; }

        public WorldSettingJsonObject GetSettingsSaveData();
        public void Initialize();
        public void LoadSettingData(WorldSettingJsonObject worldSettingJsonObject);
    }
}