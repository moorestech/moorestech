using Game.World.Interface.DataStore;
using Game.WorldMap;

namespace World.DataStore.WorldSettings
{
    public static class WorldSpawnPointSearcher
    {

        ///     ID
        ///     TODO 

        public const int IronOreId = 1;


        ///     
        ///     TODO 

        public static Coordinate SearchSpawnPoint(VeinGenerator veinGenerator)
        {
            //100,100-100,-100
            var (fond, coordinate) = SearchSpawnPointRange(veinGenerator, 100, 100, -100, -100);
            if (fond) return coordinate;

            
            (fond, coordinate) = SearchSpawnPointRange(veinGenerator, 200, 200, -500, -500);
            if (fond) return coordinate;
            // 300,300
            return new Coordinate(300, 300);
        }


        ///     2

        /// <returns></returns>
        private static (bool isFound, Coordinate coordinate) SearchSpawnPointRange(VeinGenerator veinGenerator, int x1, int y1, int x2, int y2)
        {
            if (x2 < x1) (x1, x2) = (x2, x1);
            if (y2 < y1) (y1, y2) = (y2, y1);


            for (var x = x1; x < x2; x++)
            for (var y = y1; y < y2; y++)
            {
                var veinId = veinGenerator.GetOreId(x, y);
                if (veinId == IronOreId && CheckOreExistDirection(veinGenerator, x, y, IronOreId, 2)) return (true, new Coordinate(x, y));
            }

            return (false, new Coordinate(0, 0));
        }


        ///     OreID4

        /// <returns></returns>
        private static bool CheckOreExistDirection(VeinGenerator veinGenerator, int x, int y, int oreId, int checkBlock)
        {
            for (var i = x - checkBlock; i <= x + checkBlock; i++)
            for (var j = y - checkBlock; j <= y + checkBlock; j++)
            {
                if (veinGenerator.GetOreId(i, j) == oreId) continue;

                return false;
            }

            return true;
        }
    }
}