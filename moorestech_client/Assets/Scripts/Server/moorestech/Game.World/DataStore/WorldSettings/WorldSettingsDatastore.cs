using Core.Util;
using Game.World.Interface.DataStore;
using Game.WorldMap;

namespace World.DataStore.WorldSettings
{
    /// <summary>
    ///     ワールドの基本的な設定を保持します
    ///     TODO ロード、セーブに対応させる
    /// </summary>
    public class WorldSettingsDatastore : IWorldSettingsDatastore
    {
        private readonly VeinGenerator _veinGenerator;

        public WorldSettingsDatastore(VeinGenerator veinGenerator)
        {
            _veinGenerator = veinGenerator;
        }

        public CoreVector2Int WorldSpawnPoint { get; private set; }

        public void Initialize()
        {
            WorldSpawnPoint = WorldSpawnPointSearcher.SearchSpawnPoint(_veinGenerator);
        }

        public WorldSettingSaveData GetSettingsSaveData()
        {
            return new WorldSettingSaveData(WorldSpawnPoint);
        }

        public void LoadSettingData(WorldSettingSaveData worldSettingSaveData)
        {
            WorldSpawnPoint = new CoreVector2Int(worldSettingSaveData.SpawnX, worldSettingSaveData.SpawnY);
        }
    }
}