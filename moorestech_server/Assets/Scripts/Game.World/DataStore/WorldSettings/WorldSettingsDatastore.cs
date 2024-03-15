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
        public Vector2Int WorldSpawnPoint { get; private set; }

        public void Initialize()
        {
            WorldSpawnPoint = Vector2Int.zero;
        }

        public WorldSettingSaveData GetSettingsSaveData()
        {
            return new WorldSettingSaveData(WorldSpawnPoint);
        }

        public void LoadSettingData(WorldSettingSaveData worldSettingSaveData)
        {
            WorldSpawnPoint = new Vector2Int(worldSettingSaveData.SpawnX, worldSettingSaveData.SpawnY);
        }
    }
}