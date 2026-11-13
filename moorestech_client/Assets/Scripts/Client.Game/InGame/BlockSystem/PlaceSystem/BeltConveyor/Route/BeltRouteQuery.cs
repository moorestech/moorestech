using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route
{
    /// <summary>
    ///     1回の経路探索に渡す条件。始点・終点・手持ちの性質・候補の優先順
    ///     The input of one route search: start, end, properties of the held block, and the candidate priority
    /// </summary>
    public sealed class BeltRouteQuery
    {
        public readonly Vector3Int StartCell;

        // 始点ブロックの向き。通常ベルトは出る向き、分岐器は入る向き
        // The start block's facing: the exit heading for a normal belt, the entry heading for a splitter
        public readonly BlockDirection StartDirection;

        // 分岐の始点は既存ベルトで、経路に含めず置かない。横の出口から出発する
        // A branch start is an existing belt that is neither part of the route nor placed; the route leaves from its side output
        public readonly bool StartIsExistingBelt;

        public readonly Vector3Int EndCell;
        public readonly BlockDirection EndDirection;

        // 手持ちファミリーに坂があるか。分岐器ファミリーは水平のみ
        // Whether the held family has slopes; splitter families route horizontally only
        public readonly bool AllowSlopes;

        // 分岐器手持ち。入口は背面だけ、出口は前後左右の3面で、曲がりは出口側で起きる
        // Holding a splitter: it takes input only from the back and exits through three faces, so turns happen on the exit side
        public readonly bool TurnByOutput;

        public readonly BeltRouteStepPriority Priority;

        public BeltRouteQuery(Vector3Int startCell, BlockDirection startDirection, bool startIsExistingBelt, Vector3Int endCell, BlockDirection endDirection, bool allowSlopes, bool turnByOutput, BeltRouteStepPriority priority)
        {
            StartCell = startCell;
            StartDirection = startDirection;
            StartIsExistingBelt = startIsExistingBelt;
            EndCell = endCell;
            EndDirection = endDirection;
            AllowSlopes = allowSlopes;
            TurnByOutput = turnByOutput;
            Priority = priority;
        }

        // 終点が流す先の辺。ここへ受け手を置くと終点が既存ラインへ繋がってしまうため途中では使わない
        // The edge the end flows into; a receiver there would wire the end into an existing line, so no mid-route cell may take it
        public BeltRouteEdge EndOutputEdge => BeltRouteEdge.FromFace(EndCell, EndDirection, EndCell.y);

        public bool IsSameQuery(BeltRouteQuery other)
        {
            return StartCell == other.StartCell && StartDirection == other.StartDirection && StartIsExistingBelt == other.StartIsExistingBelt &&
                   EndCell == other.EndCell && EndDirection == other.EndDirection && AllowSlopes == other.AllowSlopes &&
                   TurnByOutput == other.TurnByOutput && Priority.Equals(other.Priority);
        }
    }
}
