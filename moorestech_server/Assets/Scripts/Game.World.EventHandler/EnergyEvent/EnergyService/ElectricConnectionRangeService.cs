using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.World.EventHandler.EnergyEvent.EnergyService
{
    public static class ElectricConnectionRangeService
    {
        public static IEnumerable<Vector3Int> EnumeratePoleRange(Vector3Int center, ElectricPoleBlockParam param)
        {
            return EnumerateRange(center, param.PoleConnectionRange, param.PoleConnectionHeightRange);
        }

        public static IEnumerable<Vector3Int> EnumerateMachineRange(Vector3Int center, ElectricPoleBlockParam param)
        {
            return EnumerateRange(center, param.MachineConnectionRange, param.MachineConnectionHeightRange);
        }

        public static IEnumerable<Vector3Int> EnumerateRange(Vector3Int center, int horizontalRange, int heightRange)
        {
            var (startX, endX, startZ, endZ, startY, endY) = CreateBounds();

            for (var x = startX; x < endX; x++)
            for (var y = startY; y < endY; y++)
            for (var z = startZ; z < endZ; z++)
                yield return new Vector3Int(x, y, z);
            
            #region Iternal
            
            (int startX, int endX, int startZ, int endZ, int startY, int endY) CreateBounds()
            {
                horizontalRange = Mathf.Max(horizontalRange, 1);
                heightRange = Mathf.Max(heightRange, 1);
                
                var startXPos = center.x - horizontalRange / 2;
                var startZPos = center.z - horizontalRange / 2;
                var startYPos = center.y - heightRange / 2;
                
                var endXPos = startXPos + horizontalRange;
                var endZPos = startZPos + horizontalRange;
                var endYPos = startYPos + heightRange;
                
                return (startXPos, endXPos, startZPos, endZPos, startYPos, endYPos);
            }
            
  #endregion
        }

        public static bool IsWithinMachineRange(Vector3Int machine, Vector3Int polePosition, ElectricPoleBlockParam param)
        {
            return IsWithinRange(machine, polePosition, param.MachineConnectionRange, param.MachineConnectionHeightRange);
        }

        public static bool IsWithinPoleRange(Vector3Int target, Vector3Int polePosition, ElectricPoleBlockParam param)
        {
            return IsWithinRange(target, polePosition, param.PoleConnectionRange, param.PoleConnectionHeightRange);
        }

        private static bool IsWithinRange(Vector3Int target, Vector3Int origin, int horizontalRange, int heightRange)
        {
            return IsWithinHorizontalRange(target, origin, horizontalRange) &&
                   IsWithinHeightRange(target, origin, heightRange);
            
            #region Internal
            
            static bool IsWithinHorizontalRange(Vector3Int target, Vector3Int origin, int range)
            {
                if (range <= 0) return target.x == origin.x && target.z == origin.z;
                
                var half = range / 2;
                var minX = origin.x - half;
                var minZ = origin.z - half;
                var maxX = minX + range - 1;
                var maxZ = minZ + range - 1;
                
                return target.x >= minX && target.x <= maxX && target.z >= minZ && target.z <= maxZ;
            }
            
            static bool IsWithinHeightRange(Vector3Int target, Vector3Int origin, int range)
            {
                if (range <= 0) return target.y == origin.y;
                
                var half = range / 2;
                var minY = origin.y - half;
                var maxY = minY + range - 1;
                
                return target.y >= minY && target.y <= maxY;
            }
            
            #endregion
        }
    }
}
