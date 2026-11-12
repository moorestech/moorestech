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
            if (!PlacementAimBoxSlab.TryIntersect(ray, minCell, maxCell + Vector3Int.one, out var tEnter, out _, out var tExit, out _)) return false;
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

            // 外から入るときは入る直前のセルから走査し、入口の辺・角も走査の同値規則にまかせる
            // From outside, walk from the cell just before entry so entry edges and corners follow the walk's tie rule
            var firstCell = tEnter > 0f ? CellJustBefore(ray, tEnter) : startCell;
            return TryWalk(cells, ray, firstCell, tEnd, out distance, out surfaceType);
        }

        private static bool TryWalk(BlockOccupiedCellIndex cells, Ray ray, Vector3Int cell, float tEnd, out float distance, out PreviewSurfaceType surfaceType)
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
            var lastT = float.NaN;
            var faceAxis = -1;
            for (var axis = 0; axis < 3; axis++)
            {
                if (direction[axis] == 0f) continue;
                step[axis] = direction[axis] > 0f ? 1 : -1;
                tMax[axis] = NextBoundaryTime(origin[axis], direction[axis], cell[axis]);
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

                // 同じ t の越境（辺・角）の組では、Y → X → Z で先の軸を面の軸にする
                // Within a group of crossings at the same t (edge or corner), the earlier axis in Y -> X -> Z is the face axis
                if (t != lastT || PlacementAimBoxSlab.IsHigherPriority(nextAxis, faceAxis)) faceAxis = nextAxis;
                lastT = t;

                // 入ったセルが占有されていれば、入った向きの面を返す
                // If the entered cell is occupied, return the face of the entering direction
                if (!cells.TryGetBlockAt(cell, out _)) continue;
                distance = t;
                surfaceType = PlacementAimBoxSlab.EnterFace(faceAxis, direction[faceAxis] > 0f);
                return true;
            }
        }

        private static Vector3Int CellJustBefore(Ray ray, float t)
        {
            var origin = ray.origin;
            var direction = ray.direction;
            var cell = Vector3Int.FloorToInt(ray.GetPoint(t));
            for (var axis = 0; axis < 3; axis++)
            {
                var d = direction[axis];
                if (d == 0f) continue;

                // 境界の時刻で寄せ、次の境界が t 以上・手前の境界が t 未満のセルにする（境界ちょうどは手前側）
                // Snap by boundary times so the next boundary is at or after t and the previous one before t (exact boundary stays behind)
                var step = d > 0f ? 1 : -1;
                while (NextBoundaryTime(origin[axis], d, cell[axis]) < t) cell[axis] += step;
                while (NextBoundaryTime(origin[axis], d, cell[axis] - step) >= t) cell[axis] -= step;
            }
            return cell;
        }

        private static float NextBoundaryTime(float origin, float direction, int cell)
        {
            var boundary = direction > 0f ? cell + 1 : cell;
            return (boundary - origin) / direction;
        }
    }
}
