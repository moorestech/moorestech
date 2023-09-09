using System.Collections.Generic;
using Core.Block.Config.LoadConfig.Param;
using Core.Electric;
using Core.EnergySystem;
using Game.World.Interface.DataStore;

namespace Game.World.EventHandler.Service
{
    public static class FindElectricPoleFromPeripheralService
    {
        /// <summary>
        /// 周辺ブロックから電柱を探索します
        /// ただし、自身の電柱は含みません
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="electricPoleConfigParam"></param>
        /// <param name="blockDatastore"></param>
        /// <returns></returns>
        public static List<IEnergyTransformer> Find(
            int x,int y,
            ElectricPoleConfigParam electricPoleConfigParam,
            IWorldBlockDatastore blockDatastore)
        {
            var electricPoles = new List<IEnergyTransformer>();
            //for文のための設定
            var poleRange = electricPoleConfigParam.poleConnectionRange;
            blockDatastore.GetBlock(x, y);
            var startElectricX = x - poleRange / 2;
            var startElectricY = y - poleRange / 2;
            
            //実際の探索
            for (int i = startElectricX; i < startElectricX + poleRange; i++)
            {
                for (int j = startElectricY; j < startElectricY + poleRange; j++)
                {
                    //範囲内に電柱がある場合 ただし自身のブロックは除く
                    if (!blockDatastore.ExistsComponentBlock<IEnergyTransformer>(i, j) || i == x && j == y) continue;

                    //電柱を追加
                    electricPoles.Add(blockDatastore.GetBlock<IEnergyTransformer>(i, j));
                }
            }
            
            return electricPoles;
        }
    }
}