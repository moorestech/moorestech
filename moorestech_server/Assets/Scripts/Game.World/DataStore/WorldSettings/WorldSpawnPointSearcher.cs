using Game.WorldMap;
using UnityEngine;

namespace Game.World.DataStore.WorldSettings
{
    public static class WorldSpawnPointSearcher
    {
        /// <summary>
        ///     鉄の鉱石ID
        ///     TODO このマジックナンバーを解消する
        /// </summary>
        public const int IronOreId = 1;

        /// <summary>
        ///     ワールドの開始地点を探索する
        ///     TODO 必要になったらインジェクションするようにする
        /// </summary>
        public static Vector2Int SearchSpawnPoint(VeinGenerator veinGenerator)
        {
            //現状は100,100から-100,-100まで、鉄鉱石が生成されている場所を探索し、そこをスポーン地点とする
            var (fond, coordinate) = SearchSpawnPointRange(veinGenerator, 100, 100, -100, -100);
            if (fond) return coordinate;

            //なかった場合は、範囲を拡大して探索する
            (fond, coordinate) = SearchSpawnPointRange(veinGenerator, 200, 200, -500, -500);
            if (fond) return coordinate;
            //それでもなかった場合は 300,300をスポーン地点とする
            return new Vector2Int(300, 300);
        }

        /// <summary>
        ///     範囲を指定して鉄インゴットが上下左右に2ブロック分にある位置を探索する
        /// </summary>
        /// <returns></returns>
        private static (bool isFound, Vector2Int coordinate) SearchSpawnPointRange(VeinGenerator veinGenerator,
            int x1, int y1, int x2, int y2)
        {
            if (x2 < x1) (x1, x2) = (x2, x1);
            if (y2 < y1) (y1, y2) = (y2, y1);


            for (var x = x1; x < x2; x++)
            for (var y = y1; y < y2; y++)
            {
                var veinId = veinGenerator.GetOreId(new Vector2Int(x, y));
                if (veinId == IronOreId && CheckOreExistDirection(veinGenerator, new Vector2Int(x, y), IronOreId, 2))
                    return (true, new Vector2Int(x, y));
            }

            return (false, new Vector2Int(0, 0));
        }

        /// <summary>
        ///     指定したOreIDが4方向の、指定したブロック分存在しているかどうかをチェック
        /// </summary>
        /// <returns></returns>
        private static bool CheckOreExistDirection(VeinGenerator veinGenerator, Vector2Int pos, int oreId, int checkBlock)
        {
            var x = pos.x;
            var y = pos.y;
            
            for (var i = x - checkBlock; i <= x + checkBlock; i++)
            for (var j = y - checkBlock; j <= y + checkBlock; j++)
            {
                if (veinGenerator.GetOreId(new Vector2Int(i, j)) == oreId) continue;

                return false;
            }

            return true;
        }
    }
}