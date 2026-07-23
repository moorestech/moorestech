using System.Collections.Generic;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange
{
    public static class ElectricConnectionRangeService
    {
        /// <summary>
        /// 双方の範囲ボックスが相手の占有AABBと重なる場合のみ接続可とする相互判定
        /// Mutual judgement: connectable only when both range boxes overlap the partner's occupied AABB
        /// </summary>
        public static bool IsMutuallyConnectable(
            BlockPositionInfo aInfo, ConnectionRangeProfile aProfile, bool aIsPole,
            BlockPositionInfo bInfo, ConnectionRangeProfile bProfile, bool bIsPole)
        {
            return Covers(aInfo, aProfile.GetRangeAgainst(bIsPole), bInfo) &&
                   Covers(bInfo, bProfile.GetRangeAgainst(aIsPole), aInfo);
        }

        public static bool Covers(BlockPositionInfo self, (int Horizontal, int Height) range, BlockPositionInfo target)
        {
            var (rangeMin, rangeMax) = CreateBounds();
            return HasOverlap();

            #region Internal

            (Vector3Int min, Vector3Int max) CreateBounds()
            {
                // 占有AABBを低側floor(r/2)・高側r-1-floor(r/2)だけ膨張させる（従来のセル列挙の合併と一致）
                // Inflate the occupied AABB by floor(r/2) low and r-1-floor(r/2) high (matches the union of legacy cell enumeration)
                var horizontal = Mathf.Max(range.Horizontal, 1);
                var height = Mathf.Max(range.Height, 1);
                var lowHorizontal = horizontal / 2;
                var highHorizontal = horizontal - 1 - lowHorizontal;
                var lowHeight = height / 2;
                var highHeight = height - 1 - lowHeight;

                var min = new Vector3Int(self.MinPos.x - lowHorizontal, self.MinPos.y - lowHeight, self.MinPos.z - lowHorizontal);
                var max = new Vector3Int(self.MaxPos.x + highHorizontal, self.MaxPos.y + highHeight, self.MaxPos.z + highHorizontal);
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
