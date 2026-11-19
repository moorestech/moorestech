using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route
{
    /// <summary>
    ///     1回の経路探索に渡す条件。始点・終点・手持ちの性質
    ///     The input of one route search: start, end, and properties of the held block
    /// </summary>
    public sealed class BeltRouteQuery
    {
        public readonly Vector3Int StartCell;

        // 始点ブロックの出る向き
        // The start block's exit heading
        public readonly BlockDirection StartDirection;

        // 分岐の始点は既存ベルトで、経路に含めず置かない。横の出口から出発する
        // A branch start is an existing belt that is neither part of the route nor placed; the route leaves from its side output
        public readonly bool StartIsExistingBelt;

        public readonly Vector3Int EndCell;
        public readonly BlockDirection EndDirection;

        // 手持ちファミリーに坂があるか
        // Whether the held family has slopes
        public readonly bool AllowSlopes;

        public BeltRouteQuery(Vector3Int startCell, BlockDirection startDirection, bool startIsExistingBelt, Vector3Int endCell, BlockDirection endDirection, bool allowSlopes)
        {
            StartCell = startCell;
            StartDirection = startDirection;
            StartIsExistingBelt = startIsExistingBelt;
            EndCell = endCell;
            EndDirection = endDirection;
            AllowSlopes = allowSlopes;
        }

        public bool IsSameQuery(BeltRouteQuery other)
        {
            return StartCell == other.StartCell && StartDirection == other.StartDirection && StartIsExistingBelt == other.StartIsExistingBelt &&
                   EndCell == other.EndCell && EndDirection == other.EndDirection && AllowSlopes == other.AllowSlopes;
        }
    }
}
