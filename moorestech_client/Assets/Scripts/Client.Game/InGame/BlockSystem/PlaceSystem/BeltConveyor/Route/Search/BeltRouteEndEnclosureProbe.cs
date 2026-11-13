using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     終点から空きセルを小さく塗りつぶし、囲まれているかを見る
    ///     Flood-fills free cells a little from the end to see whether it is enclosed
    ///     囲まれた終点で上限まで探索しないためのもの。理由の区別には使わない（ADR 0100）
    ///     Keeps an enclosed end from searching up to the cap; not used to tell reasons apart (ADR 0100)
    ///     経路の隣り合うセルは水平に1歩・高さ差±1以内（坂）なので、その12近傍で塗る。真上・真下へは進まない
    ///     Consecutive route cells are one horizontal step apart with at most one level of height change (slopes), so the fill uses those 12 neighbours and never moves straight up or down
    /// </summary>
    public static class BeltRouteEndEnclosureProbe
    {
        private static readonly Vector3Int[] HorizontalSteps = { Vector3Int.right, Vector3Int.left, new(0, 0, 1), new(0, 0, -1) };

        // 訪問数が maxCells に届くか始点の隣に着けば開いている。先に待ち行列が尽きれば囲まれている
        // Open when visits reach maxCells or touch the start; enclosed when the queue drains first
        public static bool IsOpen(BeltRouteTransitionRule rule, Vector3Int endCell, int maxCells)
        {
            var startCell = rule.Query.StartCell;

            // 終点自身は呼び手が空きと判定済みなので最初に入れる
            // The end itself was already judged free by the caller, so it goes in first
            var visited = new HashSet<Vector3Int> { endCell };
            var queue = new Queue<Vector3Int>();
            queue.Enqueue(endCell);
            while (queue.Count > 0)
            {
                if (visited.Count >= maxCells) return true;
                var cell = queue.Dequeue();
                if (IsBesideStart(cell)) return true;

                // 終点の手前は同じ高さ（直線・下り坂）か1つ下（上り坂）だけ
                // The cell before the end is only at the same level (straight, down slope) or one below (up slope)
                var maxRise = cell == endCell ? 0 : 1;
                foreach (var step in HorizontalSteps)
                for (var dy = -1; dy <= maxRise; dy++)
                {
                    var next = cell + step + new Vector3Int(0, dy, 0);
                    if (visited.Contains(next) || !rule.IsCellFree(next)) continue;
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
            return visited.Count >= maxCells;

            #region Internal

            // 分岐の始点は既存ベルトで埋まっているので、始点の列か隣の列で高さ差±1以内に着けば足りる
            // A branch start is occupied by the existing belt, so reaching its column or a neighbouring one within one level suffices
            bool IsBesideStart(Vector3Int cell)
            {
                var d = cell - startCell;
                return Mathf.Abs(d.x) + Mathf.Abs(d.z) <= 1 && Mathf.Abs(d.y) <= 1;
            }

            #endregion
        }
    }
}
