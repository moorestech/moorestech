using Game.World.Interface.DataStore;
using Game.WorldMap;

namespace World.DataStore.WorldSettings
{
    /// <summary>
    /// ワールドの基本的な設定を保持します
    /// TODO ロード、セーブに対応させる
    /// </summary>
    public class WorldSettingsDatastore : IWorldSettingsDatastore
    {
        public WorldSettingsDatastore(VeinGenerator veinGenerator)
        {
            WorldSpawnPoint = WorldSpawnPointSearcher.SearchSpawnPoint(veinGenerator);
        }

        public Coordinate WorldSpawnPoint { get; }
    }
}