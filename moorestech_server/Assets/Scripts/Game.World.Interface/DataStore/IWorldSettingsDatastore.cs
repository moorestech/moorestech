using System;
using Game.Map.Interface.Json;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public interface IWorldSettingsDatastore
    {
        public Vector3 WorldSpawnPoint { get; }

        // ワールドが作られた実世界日時。進行記録がセッションの外側の文脈として読む
        // The real-world time the world was created; the progress record reads it as context outside the session
        public DateTime WorldCreationDateTimeUtc { get; }
        public TimeSpan GetCurrentPlayTime();
        
        public void Initialize(MapInfoJson mapInfoJson);
        public void LoadSettingData(WorldSettingJsonObject worldSettingJsonObject);
        public WorldSettingJsonObject GetSaveJsonObject();
    }
}