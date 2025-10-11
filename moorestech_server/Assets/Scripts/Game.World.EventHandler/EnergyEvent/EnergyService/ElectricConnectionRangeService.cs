using System.Collections.Generic;
using Game.Block.Interface;
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

        public static IEnumerable<Vector3Int> EnumerateCandidatePolePositions(BlockPositionInfo machineInfo, int horizontalRange, int heightRange)
        {
            var visited = new HashSet<Vector3Int>();

            foreach (var occupiedPos in EnumerateOccupiedPositions())
            foreach (var candidate in EnumerateRange(occupiedPos, horizontalRange, heightRange))
                if (visited.Add(candidate))
                    yield return candidate;

            #region Internal

            IEnumerable<Vector3Int> EnumerateOccupiedPositions()
            {
                for (var x = machineInfo.MinPos.x; x <= machineInfo.MaxPos.x; x++)
                for (var y = machineInfo.MinPos.y; y <= machineInfo.MaxPos.y; y++)
                for (var z = machineInfo.MinPos.z; z <= machineInfo.MaxPos.z; z++)
                    yield return new Vector3Int(x, y, z);
            }

            #endregion
        }

        public static bool IsWithinMachineRange(BlockPositionInfo machine, Vector3Int polePosition, ElectricPoleBlockParam param)
        {
            var (rangeMin, rangeMax) = CreateBounds();
            return HasOverlap();

            #region Internal

            (Vector3Int min, Vector3Int max) CreateBounds()
            {
                var horizontalRange = Mathf.Max(param.MachineConnectionRange, 1);
                var heightRange = Mathf.Max(param.MachineConnectionHeightRange, 1);

                var halfHorizontal = horizontalRange / 2;
                var halfHeight = heightRange / 2;

                var min = new Vector3Int(
                    polePosition.x - halfHorizontal,
                    polePosition.y - halfHeight,
                    polePosition.z - halfHorizontal);

                var max = new Vector3Int(
                    min.x + horizontalRange - 1,
                    min.y + heightRange - 1,
                    min.z + horizontalRange - 1);

                return (min, max);
            }

            bool HasOverlap()
            {
                return machine.MinPos.x <= rangeMax.x && rangeMin.x <= machine.MaxPos.x &&
                       machine.MinPos.y <= rangeMax.y && rangeMin.y <= machine.MaxPos.y &&
                       machine.MinPos.z <= rangeMax.z && rangeMin.z <= machine.MaxPos.z;
            }

            #endregion
        }

        public static bool IsWithinPoleRange(BlockPositionInfo target, Vector3Int polePosition, ElectricPoleBlockParam param)
        {
            var (rangeMin, rangeMax) = CreateBounds();
            return HasOverlap();

            #region Internal

            (Vector3Int min, Vector3Int max) CreateBounds()
            {
                var horizontalRange = Mathf.Max(param.PoleConnectionRange, 1);
                var heightRange = Mathf.Max(param.PoleConnectionHeightRange, 1);

                var halfHorizontal = horizontalRange / 2;
                var halfHeight = heightRange / 2;

                var min = new Vector3Int(
                    polePosition.x - halfHorizontal,
                    polePosition.y - halfHeight,
                    polePosition.z - halfHorizontal);

                var max = new Vector3Int(
                    min.x + horizontalRange - 1,
                    min.y + heightRange - 1,
                    min.z + horizontalRange - 1);

                return (min, max);
            }

            bool HasOverlap()
            {
                return target.MinPos.x <= rangeMax.x && rangeMin.x <= target.MaxPos.x &&
                       target.MinPos.y <= rangeMax.y && rangeMin.y <= target.MaxPos.y &&
                       target.MinPos.z <= rangeMax.z && rangeMin.z <= target.MaxPos.z;
            }

            #endregion
        }
    }
}
