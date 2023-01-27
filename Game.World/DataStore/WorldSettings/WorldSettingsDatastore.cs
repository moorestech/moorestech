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
        public Coordinate WorldSpawnPoint { get; private set;}
        
        private readonly VeinGenerator _veinGenerator;
        public WorldSettingsDatastore(VeinGenerator veinGenerator)
        {
            _veinGenerator = veinGenerator;
        }

        public void Initialize()
        {
            WorldSpawnPoint = WorldSpawnPointSearcher.SearchSpawnPoint(_veinGenerator);
        }

        public WorldSettingSaveData Save()
        {
            return new WorldSettingSaveData(WorldSpawnPoint);
        }

        public void Load(WorldSettingSaveData worldSettingSaveData)
        {
            WorldSpawnPoint = new Coordinate(worldSettingSaveData.SpawnX, worldSettingSaveData.SpawnY);
        }
    }
}