using System;
using System.Globalization;
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

        private DateTime _worldCreationDateTimeUtc;
        private bool _hasWorldCreationDateTime;
        private double _totalPlayTimeSeconds;
        private DateTime _currentSessionStartDateTime;

        public void Initialize(MapInfoJson mapInfoJson)
        {
            WorldSpawnPoint = mapInfoJson.DefaultSpawnPointJson.Position;

            _worldCreationDateTimeUtc = DateTime.UtcNow;
            _hasWorldCreationDateTime = true;
            _totalPlayTimeSeconds = 0;
            _currentSessionStartDateTime = DateTime.UtcNow;
        }
        
        public void LoadSettingData(WorldSettingJsonObject json)
        {
            WorldSpawnPoint = new Vector3(json.SpawnX, json.SpawnY, json.SpawnZ);
            _totalPlayTimeSeconds = json.TotalPlayTimeSeconds;
            _currentSessionStartDateTime = DateTime.UtcNow;
            
            if (string.IsNullOrEmpty(json.WorldCreationDateTime))
            {
                // 作成日時が欠けたセーブは既定値のまま進む。無言だと保存時に別日時が書かれる理由が追えない
                // A save without a creation time keeps the default; staying silent would hide why a different time gets written back
                Debug.LogWarning("セーブに世界作成日時が無いため既定値のままロードします");
                return;
            }

            // RoundtripKindを付けないとUTC保存がローカル時刻へ倒れ、保存し直すと同じ瞬間が別表記になる（再生の忠実性が壊れる）
            // Without RoundtripKind a UTC save falls back to local time and re-saving writes the same instant in a different notation, breaking replay fidelity
            _worldCreationDateTimeUtc = DateTime.Parse(json.WorldCreationDateTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            _hasWorldCreationDateTime = true;
        }

        public bool TryGetWorldCreationDateTimeUtc(out DateTime worldCreationDateTimeUtc)
        {
            worldCreationDateTimeUtc = _worldCreationDateTimeUtc;
            return _hasWorldCreationDateTime;
        }

        public WorldSettingJsonObject GetSaveJsonObject()
        {
            var currentPlayTime = GetCurrentPlayTime();

            var json = new WorldSettingJsonObject(WorldSpawnPoint, _worldCreationDateTimeUtc, currentPlayTime, DateTime.UtcNow);

            // 欠損は欠損のまま保存する。既定値を書くと次のロードで0001年が実日時として読まれる
            // A missing creation time is saved as missing; writing the default would be read back as a real year-0001 time on the next load
            if (!_hasWorldCreationDateTime) json.WorldCreationDateTime = null;
            return json;
        }

        public TimeSpan GetCurrentPlayTime()
        {
            var currentSessionTime = DateTime.UtcNow - _currentSessionStartDateTime;
            var totalTime = TimeSpan.FromSeconds(_totalPlayTimeSeconds) + currentSessionTime;
            return totalTime;
        }
    }
}