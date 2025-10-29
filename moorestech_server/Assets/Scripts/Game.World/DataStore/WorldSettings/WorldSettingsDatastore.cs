using System;
using Game.Map.Interface.Json;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.World.DataStore.WorldSettings
{
    /// <summary>
    ///     ワールドの基本的な設定を保持します
    ///     TODO ロード、セーブに対応させる
    /// </summary>
    public class WorldSettingsDatastore : IWorldSettingsDatastore
    {
        public Vector3 WorldSpawnPoint { get; private set; }
        
        private DateTime _worldCreationDateTime;
        private double _totalPlayTimeSeconds;
        private DateTime _currentSessionStartDateTime;

        public void Initialize(MapInfoJson mapInfoJson)
        {
            WorldSpawnPoint = mapInfoJson.DefaultSpawnPointJson.Position;
            
            _worldCreationDateTime = DateTime.UtcNow;
            _totalPlayTimeSeconds = 0;
            _currentSessionStartDateTime = DateTime.UtcNow;
        }
        
        public void LoadSettingData(WorldSettingJsonObject json)
        {
            WorldSpawnPoint = new Vector3(json.SpawnX, json.SpawnY, json.SpawnZ);
            _totalPlayTimeSeconds = json.TotalPlayTimeSeconds;
            _currentSessionStartDateTime = DateTime.UtcNow;
            
            if (!string.IsNullOrEmpty(json.WorldCreationDateTime))
            {
                _worldCreationDateTime = DateTime.Parse(json.WorldCreationDateTime);
            }
        }

        public WorldSettingJsonObject GetSaveJsonObject()
        {
            var currentPlayTime = GetCurrentPlayTime();
            
            return new WorldSettingJsonObject(WorldSpawnPoint, _worldCreationDateTime, currentPlayTime, DateTime.UtcNow);
        }

        public TimeSpan GetCurrentPlayTime()
        {
            var currentSessionTime = DateTime.UtcNow - _currentSessionStartDateTime;
            var totalTime = TimeSpan.FromSeconds(_totalPlayTimeSeconds) + currentSessionTime;
            return totalTime;
        }
    }
}