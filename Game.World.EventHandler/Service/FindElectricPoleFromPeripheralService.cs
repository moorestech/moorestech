using System.Collections.Generic;
using Core.Block.Config.LoadConfig.Param;
using Core.Electric;
using Game.World.Interface.DataStore;

namespace Game.World.EventHandler.Service
{
    public class FindElectricPoleFromPeripheralService
    {
        /// <summary>
        /// 周辺ブロックから電柱を探索します
        /// ただし、自身の電柱は含みません
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="electricPoleConfigParam"></param>
        /// <param name="electricPoleDatastore"></param>
        /// <returns></returns>
        public List<IElectricPole> Find(
            int x,int y,
            ElectricPoleConfigParam electricPoleConfigParam,
            IWorldBlockComponentDatastore<IElectricPole> electricPoleDatastore)
        {
            var electricPoles = new List<IElectricPole>();
            //for文のための設定
            var poleRange = electricPoleConfigParam.poleConnectionRange;
            electricPoleDatastore.GetBlock(x, y);
            var startElectricX = x - poleRange / 2;
            var startElectricY = y - poleRange / 2;
            
            //実際の探索
            for (int i = startElectricX; i < startElectricX + poleRange; i++)
            {
                for (int j = startElectricY; j < startElectricY + poleRange; j++)
                {
                    //範囲内に電柱がある場合 ただし自身のブロックは除く
                    if (!electricPoleDatastore.ExistsComponentBlock(i, j) || i == x && j == y) continue;

                    //電柱を追加
                    electricPoles.Add(electricPoleDatastore.GetBlock(i, j));
                }
            }
            
            return electricPoles;
        }
    }
}