using System.Collections.Generic;
using Core.EnergySystem;
using Game.Block.Config.LoadConfig.Param;
using Game.World.Interface.DataStore;

namespace Game.World.EventHandler.Service
{
    public static class FindElectricPoleFromPeripheralService
    {

        ///     
        ///     

        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="electricPoleConfigParam"></param>
        /// <param name="blockDatastore"></param>
        /// <returns></returns>
        public static List<IEnergyTransformer> Find(
            int x, int y,
            ElectricPoleConfigParam electricPoleConfigParam,
            IWorldBlockDatastore blockDatastore)
        {
            var electricPoles = new List<IEnergyTransformer>();
            //for
            var poleRange = electricPoleConfigParam.poleConnectionRange;
            blockDatastore.GetBlock(x, y);
            var startElectricX = x - poleRange / 2;
            var startElectricY = y - poleRange / 2;

            
            for (var i = startElectricX; i < startElectricX + poleRange; i++)
            for (var j = startElectricY; j < startElectricY + poleRange; j++)
            {
                // 
                if (!blockDatastore.ExistsComponentBlock<IEnergyTransformer>(i, j) || (i == x && j == y)) continue;

                
                electricPoles.Add(blockDatastore.GetBlock<IEnergyTransformer>(i, j));
            }

            return electricPoles;
        }
    }
}