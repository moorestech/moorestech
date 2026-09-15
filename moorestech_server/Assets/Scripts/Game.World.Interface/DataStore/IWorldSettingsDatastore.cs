using System;
using Game.Map.Interface.Json;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    public interface IWorldSettingsDatastore
    {
        public Vector3 WorldSpawnPoint { get; }

        // ワールドが作られた実世界日時。作成日時の無いセーブでは false を返し、既定値を実日時として名乗らない
        // The real-world time the world was created; a save without one returns false instead of passing the default off as a real time
        public bool TryGetWorldCreationDateTimeUtc(out DateTime worldCreationDateTimeUtc);
        public TimeSpan GetCurrentPlayTime();
        
        public void Initialize(MapInfoJson mapInfoJson);
        public void LoadSettingData(WorldSettingJsonObject worldSettingJsonObject);
        public WorldSettingJsonObject GetSaveJsonObject();
    }
}