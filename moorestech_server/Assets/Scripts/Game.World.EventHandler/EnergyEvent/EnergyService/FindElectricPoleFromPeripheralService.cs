using System.Collections.Generic;
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
        /// <param name="electricPoleConfigParam"></param>
        /// <returns></returns>
        public static List<IElectricTransformer> Find(Vector3Int pos, ElectricPoleBlockParam electricPoleConfigParam)
        {
            var electricPoles = new List<IElectricTransformer>();
            var blockDatastore = ServerContext.WorldBlockDatastore;

            foreach (var targetPos in EnumerateRange(pos, electricPoleConfigParam.PoleConnectionRange,
                         electricPoleConfigParam.PoleConnectionHeightRange))
            {
                if (!blockDatastore.ExistsComponent<IElectricTransformer>(targetPos) || targetPos == pos) continue;

                electricPoles.Add(blockDatastore.GetBlock<IElectricTransformer>(targetPos));
            }
            
            return electricPoles;

            #region Internal

            static IEnumerable<Vector3Int> EnumerateRange(Vector3Int center, int horizontalRange, int heightRange)
            {
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

            #endregion
        }
    }
}
