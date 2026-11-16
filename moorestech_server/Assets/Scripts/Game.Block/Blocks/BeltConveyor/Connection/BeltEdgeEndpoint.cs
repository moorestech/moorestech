using System.Collections.Generic;
using Game.Block.Interface;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Connection
{
    internal static class BeltEdgeEndpoint
    {
        internal static List<BeltEdge> GetEdges(BlockPositionInfo position, BeltConveyorSlopeType slope)
        {
            var cell = position.OriginalPos;
            var forward = position.BlockDirection.ConvertLocalCell(Vector3Int.forward);
            var result = new List<BeltEdge>();

            // 傾斜は搬送軸両端だけ、水平は底面四辺に接する
            // Slopes touch only their two transport ends; flat belts touch all four bottom edges
            if (slope != BeltConveyorSlopeType.Straight)
            {
                result.Add(new BeltEdge(cell, -forward, cell.y + (slope == BeltConveyorSlopeType.Down ? 1 : 0)));
                result.Add(new BeltEdge(cell, forward, cell.y + (slope == BeltConveyorSlopeType.Up ? 1 : 0)));
                return result;
            }
            result.Add(new BeltEdge(cell, Vector3Int.right, cell.y));
            result.Add(new BeltEdge(cell, Vector3Int.left, cell.y));
            result.Add(new BeltEdge(cell, Vector3Int.forward, cell.y));
            result.Add(new BeltEdge(cell, Vector3Int.back, cell.y));
            return result;
        }
    }
}
