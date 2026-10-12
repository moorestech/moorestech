using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Blueprint;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.Blueprint.Planning
{
    internal sealed class BlueprintPasteCopyOccupancy
    {
        internal IReadOnlyList<BlockPositionInfo> Positions { get; }
        private readonly Vector3Int _min;
        private readonly Vector3Int _max;

        private BlueprintPasteCopyOccupancy(List<BlockPositionInfo> positions)
        {
            Positions = positions;
            if (positions.Count == 0) return;
            _min = positions[0].MinPos;
            _max = positions[0].MaxPos;
            foreach (var position in positions)
            {
                _min = Vector3Int.Min(_min, position.MinPos);
                _max = Vector3Int.Max(_max, position.MaxPos);
            }
        }

        internal static BlueprintPasteCopyOccupancy Create(IReadOnlyList<BlueprintPlacementElement> elements)
        {
            var positions = new List<BlockPositionInfo>(elements.Count);
            foreach (var element in elements)
                positions.Add(BlueprintPlacementElementUtil.ToPositionInfo(element));
            return new BlueprintPasteCopyOccupancy(positions);
        }

        internal BlueprintPasteCopyOccupancy SelectPlaced(IReadOnlyList<bool> nonOverlapFlags)
        {
            // 受理した箱を再利用し、空白は予約しない
            // Reuse accepted boxes without reserving gaps
            var positions = new List<BlockPositionInfo>();
            for (var i = 0; i < Positions.Count; i++)
                if (nonOverlapFlags[i]) positions.Add(Positions[i]);
            return new BlueprintPasteCopyOccupancy(positions);
        }

        internal List<BlueprintPasteCopyOccupancy> FindIntersecting(IReadOnlyList<BlueprintPasteCopyOccupancy> accepted)
        {
            var intersecting = new List<BlueprintPasteCopyOccupancy>();
            if (Positions.Count == 0) return intersecting;
            foreach (var copy in accepted)
            {
                // コピー全体が離れていれば個別箱を調べない
                // Skip individual boxes when whole copies are separated
                if (_min.x <= copy._max.x && copy._min.x <= _max.x &&
                    _min.y <= copy._max.y && copy._min.y <= _max.y &&
                    _min.z <= copy._max.z && copy._min.z <= _max.z)
                    intersecting.Add(copy);
            }
            return intersecting;
        }

        internal bool Overlaps(BlockPositionInfo candidate)
        {
            foreach (var position in Positions)
                if (candidate.IsOverlap(position)) return true;
            return false;
        }
    }
}
