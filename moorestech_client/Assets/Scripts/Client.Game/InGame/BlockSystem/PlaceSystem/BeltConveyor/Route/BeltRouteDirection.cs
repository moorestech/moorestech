using System;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route
{
    /// <summary>
    ///     探索の内側で使う水平4方向の表引き。行列回転を避けて毎フレームの探索を軽くする
    ///     Table lookups for the four headings used inside the search, avoiding matrix rotation per step
    /// </summary>
    public static class BeltRouteDirection
    {
        public const int DirectionCount = 4;

        // North=+Z, East=+X, South=-Z, West=-X（BlockDirectionの回転定義と一致）
        // North=+Z, East=+X, South=-Z, West=-X, matching BlockDirection's rotation definition
        private static readonly Vector3Int[] Forwards =
        {
            new(0, 0, 1),
            new(1, 0, 0),
            new(0, 0, -1),
            new(-1, 0, 0),
        };

        public static Vector3Int Forward(BlockDirection direction)
        {
            return Forwards[(int)direction];
        }

        public static BlockDirection Right(BlockDirection direction)
        {
            return (BlockDirection)(((int)direction + 1) % DirectionCount);
        }

        public static BlockDirection Left(BlockDirection direction)
        {
            return (BlockDirection)(((int)direction + 3) % DirectionCount);
        }

        public static BlockDirection Opposite(BlockDirection direction)
        {
            return (BlockDirection)(((int)direction + 2) % DirectionCount);
        }

        public static bool IsXAxis(BlockDirection direction)
        {
            return direction == BlockDirection.East || direction == BlockDirection.West;
        }

        // 水平の単位差分から向きを戻す。斜めや零は呼び出し側の契約違反
        // Recovers a heading from a unit horizontal delta; diagonal or zero deltas violate the caller's contract
        public static BlockDirection FromStep(Vector3Int step)
        {
            for (var i = 0; i < DirectionCount; i++)
            {
                if (Forwards[i].x == step.x && Forwards[i].z == step.z) return (BlockDirection)i;
            }
            throw new ArgumentException($"BeltRouteDirection: not a unit horizontal step {step}");
        }
    }
}
