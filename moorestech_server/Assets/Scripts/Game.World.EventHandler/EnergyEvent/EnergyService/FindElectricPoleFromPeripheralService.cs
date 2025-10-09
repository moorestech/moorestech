using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Context;
using Game.EnergySystem;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;
using static Game.World.EventHandler.EnergyEvent.EnergyService.ElectricConnectionRangeService;

namespace Game.World.EventHandler.EnergyEvent.EnergyService
{
    public static class FindElectricPoleFromPeripheralService
    {
        /// <summary>
        ///     周辺ブロックから電柱を探索します
        ///     ただし、自身の電柱は含みません
        /// </summary>
        public static List<IElectricTransformer> Find(Vector3Int pos, ElectricPoleBlockParam param)
        {
            var electricPoles = new Dictionary<BlockInstanceId,IElectricTransformer>();
            var blockDatastore = ServerContext.WorldBlockDatastore;
            var selfInstanceId = blockDatastore.GetBlock<IElectricTransformer>(pos).BlockInstanceId;

            foreach (var targetPos in EnumeratePoleRange(pos, param))
            {
                if(!blockDatastore.TryGetBlock<IElectricTransformer>(targetPos,out var poleBlock)) continue;
                if (poleBlock.BlockInstanceId == selfInstanceId) continue;
                
                electricPoles.TryAdd(poleBlock.BlockInstanceId, poleBlock);
            }
            
            return electricPoles.Values.ToList();

        }
    }
}
