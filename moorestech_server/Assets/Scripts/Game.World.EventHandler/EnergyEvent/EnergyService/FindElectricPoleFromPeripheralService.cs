using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Context;
using Game.EnergySystem;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
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
        /// <param name="param"></param>
        /// <returns></returns>
        public static List<IElectricTransformer> Find(Vector3Int pos, ElectricPoleBlockParam param)
        {
            var electricPoles = new Dictionary<BlockInstanceId,IElectricTransformer>();
            var blockDatastore = ServerContext.WorldBlockDatastore;
            var selfInstanceId = blockDatastore.GetBlock<IElectricTransformer>(pos).BlockInstanceId;

            foreach (var targetPos in EnumerateRange(pos, param))
            {
                if(!blockDatastore.TryGetBlock<IElectricTransformer>(targetPos,out var poleBlock)) continue;
                if (poleBlock.BlockInstanceId == selfInstanceId) continue;
                
                electricPoles.TryAdd(poleBlock.BlockInstanceId, poleBlock);
            }
            
            return electricPoles.Values.ToList();

        }
        public static IEnumerable<Vector3Int> EnumerateRange(Vector3Int center, ElectricPoleBlockParam param)
        {
            var horizontalRange = param.PoleConnectionRange;
            var heightRange = param.PoleConnectionHeightRange;
            
            horizontalRange = Mathf.Max(horizontalRange, 1);
            heightRange = Mathf.Max(heightRange, 1);
            
            var startX = center.x - horizontalRange / 2;
            var startZ = center.z - horizontalRange / 2;
            var startY = center.y - heightRange / 2;
            
            var endX = startX + horizontalRange;
            var endZ = startZ + horizontalRange;
            var endY = startY + heightRange;
            
            for (var x = startX; x < endX; x++)
            for (var y = startY; y < endY; y++)
            for (var z = startZ; z < endZ; z++)
                yield return new Vector3Int(x, y, z);
        }
    }
}
