using Game.Map.Interface.Json;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public interface IWorldSettingsDatastore
    {
        public Vector3 WorldSpawnPoint { get; }
        
        public WorldSettingJsonObject GetSaveJsonObject();
        public void Initialize(MapInfoJson mapInfoJson);
        public void LoadSettingData(WorldSettingJsonObject worldSettingJsonObject);
    }
}