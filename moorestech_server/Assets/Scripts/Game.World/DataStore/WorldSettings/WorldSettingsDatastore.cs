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
        public static readonly Vector3 GameDefaultSpawnPoint = new(186, 15.7f, -37.401f);
        
        public Vector3 WorldSpawnPoint { get; private set; }
        
        public void Initialize()
        {
            WorldSpawnPoint = GameDefaultSpawnPoint;
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