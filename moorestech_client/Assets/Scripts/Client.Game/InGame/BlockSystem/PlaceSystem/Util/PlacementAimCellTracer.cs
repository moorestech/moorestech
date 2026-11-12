using Client.Game.InGame.Block.Index;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 照準レイに沿って占有セルをたどり、最初に入るブロックの面を求める（ADR 0100）
    /// Walks occupied cells along the aim ray and resolves the face of the first block entered (ADR 0100)
    /// </summary>
    public static class PlacementAimCellTracer
    {
        public static bool TryTrace(BlockOccupiedCellIndex cells, Ray ray, float maxDistance, out float distance, out PreviewSurfaceType surfaceType)
        {
            distance = 0f;
            surfaceType = default;

            // 占有セル全体の外接直方体とレイの区間を求める
            // Clip the ray against the bounding box of all occupied cells
            // silent-ok: ブロックが無い・照準が外れているのは操作中の通常状態 / 呼び出し側が物理レイの結果へ戻す
            // silent-ok: no blocks or a missed aim is a normal aim state / the caller falls back to the physics result
            if (!cells.TryGetBounds(out var minCell, out var maxCell)) return false;
            if (!PlacementAimBoxSlab.TryIntersect(ray, minCell, maxCell + Vector3Int.one, out var tEnter, out var enterAxis, out var tExit, out _)) return false;
            var tEnd = Mathf.Min(tExit, maxDistance);

            // 始点がブロックの内側なら、そのブロックから出る面を返す
            // When the origin is inside a block, return the face it exits through
            var startCell = Vector3Int.FloorToInt(ray.origin);
            if (cells.TryGetBlockAt(startCell, out var startBlock))
            {
                // 始点がこの箱の内側にあるので交差は必ず成立し、結果は捨ててよい
                // The origin lies inside this box, so the intersection always succeeds and the result can be ignored
                PlacementAimBoxSlab.TryIntersect(ray, startBlock.MinPos, startBlock.MaxPos + Vector3Int.one, out _, out _, out var tLeave, out var exitAxis);
                // silent-ok: 射程内でブロックから出ないのは照準先にブロックが無い通常状態 / 呼び出し側が物理レイの結果へ戻す
                // silent-ok: not leaving the block within range means no block on the aim path / the caller falls back to the physics result
                if (tLeave > tEnd) return false;
                distance = tLeave;
                surfaceType = PlacementAimBoxSlab.ExitFace(exitAxis, ray.direction[exitAxis] > 0f);
                return true;
            }

            // 外から入るときは、入る点のセルを外接直方体の中へ収めて最初のセルにする
            // From outside, the entry point's cell clamped into the bounds is the first cell
            if (tEnter <= 0f) return TryWalk(cells, ray, startCell, tEnd, float.NaN, -1, out distance, out surfaceType);
            // silent-ok: 射程が外接直方体に届かないのは照準先にブロックが無い通常状態 / 呼び出し側が物理レイの結果へ戻す
            // silent-ok: range ending before the bounds means no block on the aim path / the caller falls back to the physics result
            if (tEnter > tEnd) return false;
            var entryCell = Vector3Int.Min(Vector3Int.Max(Vector3Int.FloorToInt(ray.GetPoint(tEnter)), minCell), maxCell);
            if (cells.TryGetBlockAt(entryCell, out _))
            {
                distance = tEnter;
                surfaceType = PlacementAimBoxSlab.EnterFace(enterAxis, ray.direction[enterAxis] > 0f);
                return true;
            }
            return TryWalk(cells, ray, entryCell, tEnd, tEnter, enterAxis, out distance, out surfaceType);
        }

        private static bool TryWalk(BlockOccupiedCellIndex cells, Ray ray, Vector3Int cell, float tEnd, float lastT, int faceAxis, out float distance, out PreviewSurfaceType surfaceType)
        {
            distance = 0f;
            surfaceType = default;

            // 各軸の進む向き・次の境界までの距離・1セル分の距離を用意する
            // Prepare per-axis step, distance to the next boundary, and distance per cell
            var origin = ray.origin;
            var direction = ray.direction;
            var step = Vector3Int.zero;
            var tMax = Vector3.positiveInfinity;
            var tDelta = Vector3.positiveInfinity;
            for (var axis = 0; axis < 3; axis++)
            {
                if (direction[axis] == 0f) continue;
                step[axis] = direction[axis] > 0f ? 1 : -1;
                var boundary = direction[axis] > 0f ? cell[axis] + 1 : cell[axis];
                tMax[axis] = (boundary - origin[axis]) / direction[axis];
                tDelta[axis] = 1f / Mathf.Abs(direction[axis]);
            }

            while (true)
            {
                // 次の境界が最も近い軸へ進む（同値は Y → X → Z）
                // Step along the axis with the nearest boundary (ties: Y -> X -> Z)
                var nextAxis = PlacementAimBoxSlab.NearestAxis(tMax);
                var t = tMax[nextAxis];
                // silent-ok: 射程内に占有セルが無いのは照準先にブロックが無い通常状態 / 呼び出し側が物理レイの結果へ戻す
                // silent-ok: no occupied cell within range means no block on the aim path / the caller falls back to the physics result
                if (float.IsInfinity(t) || t > tEnd) return false;
                cell[nextAxis] += step[nextAxis];
                tMax[nextAxis] += tDelta[nextAxis];

                // 同じ t の越境（辺・角）は、その組の最初の軸を面の軸として保つ
                // Crossings at the same t (edge or corner) keep the first axis of that group as the face axis
                if (t != lastT) faceAxis = nextAxis;
                lastT = t;

                // 入ったセルが占有されていれば、入った向きの面を返す
                // If the entered cell is occupied, return the face of the entering direction
                if (!cells.TryGetBlockAt(cell, out _)) continue;
                distance = t;
                surfaceType = PlacementAimBoxSlab.EnterFace(faceAxis, direction[faceAxis] > 0f);
                return true;
            }
        }
    }
}
