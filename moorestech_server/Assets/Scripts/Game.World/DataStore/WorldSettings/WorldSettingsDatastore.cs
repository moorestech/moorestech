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
        
        public void Initialize(MapInfoJson mapInfoJson)
        {
            WorldSpawnPoint = mapInfoJson.DefaultSpawnPointJson.Position;
        }
        
        public WorldSettingJsonObject GetSaveJsonObject()
        {
            return new WorldSettingJsonObject(WorldSpawnPoint);
        }
        
        public void LoadSettingData(WorldSettingJsonObject worldSettingJsonObject)
        {
            WorldSpawnPoint = new Vector3(worldSettingJsonObject.SpawnX, worldSettingJsonObject.SpawnY, worldSettingJsonObject.SpawnZ);
        }
    }
}