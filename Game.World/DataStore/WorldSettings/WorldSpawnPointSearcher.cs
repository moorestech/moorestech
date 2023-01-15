using Game.World.Interface.DataStore;
using Game.WorldMap;

namespace World.DataStore.WorldSettings
{
    public static class WorldSpawnPointSearcher
    {
        /// <summary>
        /// 鉄の鉱石ID
        /// TODO このマジックナンバーを解消する
        /// </summary>
        private const int IronOreId = 1;
        
        /// <summary>
        /// ワールドの開始地点を探索する
        /// TODO 必要になったらインジェクションするようにする
        /// </summary>
        public static Coordinate SearchSpawnPoint(VeinGenerator veinGenerator)
        {
            //現状は0,0から500,500まで、鉄鉱石が生成されている場所を探索し、そこをスポーン地点とする
            for (int i = 0; i < 500; i++)
            {
                for (int j = 0; j < 500; j++)
                {
                    var oreId = veinGenerator.GetOreId(i, j);
                    if (oreId != IronOreId) continue;
                    
                    return new Coordinate(i, j);
                }
            }

            //なかったので0,0を返す
            return new Coordinate(0,0);
        }
    }
}