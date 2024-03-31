using System.Collections.Generic;
using Server.Core.EnergySystem;
using Game.Block.Config.LoadConfig.Param;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.World.EventHandler.EnergyEvent.EnergyService
{
    public static class FindElectricPoleFromPeripheralService
    {
        /// <summary>
        ///     周辺ブロックから電柱を探索します
        ///     ただし、自身の電柱は含みません
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="electricPoleConfigParam"></param>
        /// <param name="blockDatastore"></param>
        /// <returns></returns>
        public static List<IEnergyTransformer> Find(
            Vector3Int pos,
            ElectricPoleConfigParam electricPoleConfigParam,
            IWorldBlockDatastore blockDatastore)
        {
            var electricPoles = new List<IEnergyTransformer>();
            //for文のための設定
            var poleRange = electricPoleConfigParam.poleConnectionRange;
            blockDatastore.GetBlock(pos);
            var startElectricX = pos.x - poleRange / 2;
            var startElectricY = pos.y - poleRange / 2;

            //実際の探索
            for (var i = startElectricX; i < startElectricX + poleRange; i++)
            for (var j = startElectricY; j < startElectricY + poleRange; j++)
            {
                //範囲内に電柱がある場合 ただし自身のブロックは除く
                var electricPolePos = new Vector3Int(i, j);
                if (!blockDatastore.ExistsComponent<IEnergyTransformer>(electricPolePos) || i == pos.x && j == pos.y) continue;

                //電柱を追加
                electricPoles.Add(blockDatastore.GetBlock<IEnergyTransformer>(electricPolePos));
            }

            return electricPoles;
        }
    }
}