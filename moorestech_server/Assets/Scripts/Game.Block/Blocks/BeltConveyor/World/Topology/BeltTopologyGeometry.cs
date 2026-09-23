using System;
using Game.BeltSegment;
using UnityEngine;
namespace Game.Block.Blocks.BeltConveyor
{
    internal static class BeltTopologyGeometry
    {
        internal static int Compare(Vector3Int a, Vector3Int b)
        {
            int c = a.x.CompareTo(b.x); if (c != 0) return c;
            c = a.z.CompareTo(b.z); return c != 0 ? c : a.y.CompareTo(b.y);
        }
        internal static BeltDirection Direction(Vector3Int delta)
        {
            if (delta.z > 0) return BeltDirection.Front;
            if (delta.z < 0) return BeltDirection.Back;
            if (delta.x < 0) return BeltDirection.Left;
            if (delta.x > 0) return BeltDirection.Right;
            throw new InvalidOperationException("Belt edges require a horizontal direction.");
        }
        internal static BeltRouteCell Cell(SegmentBeltComponent belt, Vector3Int upstream)
        {
            var p = belt.Position.OriginalPos;
            int elevation = upstream.y > p.y ? 4 : upstream.y < p.y ? 8 : 0;
            var entry = (BeltEntryDirection)((int)Direction(upstream - p) + elevation);
            return new BeltRouteCell(new BeltCell(p.x, p.z, p.y), entry,
                p.y * 2 + (belt.SlopeType == BeltConveyorSlopeType.Straight ? 0 : 1),
                p.y * 2 + (belt.SlopeType == BeltConveyorSlopeType.Down ? 2 : 0));
        }
        internal static BeltRouteCell ExternalCell(Vector3Int cell)
            => new(new BeltCell(cell.x, cell.z, cell.y), BeltEntryDirection.FromBack, cell.y * 2, cell.y * 2);
    }
}
