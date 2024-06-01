using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public interface IWorldSettingsDatastore
    {
        public Vector3Int WorldSpawnPoint { get; }

        public WorldSettingJsonObject GetSaveJsonObject();
        public void Initialize();
        public void LoadSettingData(WorldSettingJsonObject worldSettingJsonObject);
    }
}