using System.Collections.Generic;
using Game.Block.Interface;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Connection
{
    internal static class BeltEdgeEndpoint
    {
        internal static List<BeltEdge> GetEdges(BlockPositionInfo position, BeltConveyorSlopeType slope)
        {
            // 水平姿勢だけが共有edgeを持つ。縦置きはプレイヤーが普通に置けるのでログなしで空にする
            // Only horizontal orientations get shared edges; players can place belts vertically, so return none silently
            if (position.BlockDirection is not (BlockDirection.North or BlockDirection.East or BlockDirection.South or BlockDirection.West)) return new List<BeltEdge>();

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
