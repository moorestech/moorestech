using System;
using Game.Map.Interface.Json;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public interface IWorldSettingsDatastore
    {
        public Vector3 WorldSpawnPoint { get; }
        public TimeSpan GetCurrentPlayTime();
        
        public void Initialize(MapInfoJson mapInfoJson);
        public void LoadSettingData(WorldSettingJsonObject worldSettingJsonObject);
        public WorldSettingJsonObject GetSaveJsonObject();
    }
}