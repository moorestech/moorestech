using System.Collections.Generic;
using Core.Master;
using UnityEngine;

namespace Game.CleanRoom
{
    // 新検出部屋へ旧状態を最大重なりで引き継ぎ、対応しない旧状態を孤立化する純粋ロジック（全走査/差分更新の共通核）。
    // Pure carry-over: inherit old state onto new rooms by max overlap; unmatched old states become orphans (shared by full/incremental paths).
    public static class CleanRoomCarryOver
    {
        // 新検出部屋へ旧状態プールを引き継ぎ、対応しなかった旧状態は orphans へ積む。
        // Carry old-state pool onto new rooms; unmatched old states are appended to orphans.
        public static void ApplyCarryOver(List<CleanRoom> newRooms, List<CleanRoom> pool, List<CleanRoom> orphans)
        {
            var outIndex = MasterHolder.CleanRoomThresholdMaster.OutThresholdIndex;
            var matched = new HashSet<CleanRoom>();

            foreach (var room in newRooms)
            {
                // 重なる旧状態の寄与を合算し、最大重なりの旧状態から行を引き継ぐ。
                // Sum N contributions; carry the threshold row from the max-overlap old room.
                var carriedN = 0.0;
                CleanRoom best = null;
                var bestOverlap = 0;
                foreach (var old in pool)
                {
                    var overlap = CountOverlap(old.Cells, room);
                    if (overlap <= 0) continue;
                    matched.Add(old);
                    carriedN += CleanRoomPurityRules.RedistributeImpurity(old.ImpurityCount, old.Cells.Count, overlap);
                    if (overlap > bestOverlap) { bestOverlap = overlap; best = old; }
                }

                // 新規部屋は Out で開始。重なりがあれば最大重なり旧部屋の行を引き継ぐ。
                // Fresh rooms get Out; rooms with overlap inherit the best old room's row.
                room.SetThresholdIndex(best != null ? best.ThresholdIndex : outIndex);
                if (carriedN > 0.0) room.AddImpurity(carriedN);
                room.SetStatus(CleanRoomRoomStatus.Valid, 0.0);
            }

            // どの新部屋にも対応しなかった旧状態は孤立へ: Valid→Degraded＋猶予開始、Degraded→猶予継続。
            // Unmatched old states become orphans: Valid -> Degraded with fresh grace; Degraded keeps its grace.
            foreach (var old in pool)
            {
                if (matched.Contains(old)) continue;
                if (old.Status == CleanRoomRoomStatus.Valid)
                    old.SetStatus(CleanRoomRoomStatus.Degraded, CleanRoomPurityRules.GraceSeconds);
                orphans.Add(old);
            }
        }

        // 旧状態のセル集合と新部屋の重なりセル数。
        // Overlapping cell count between an old room and a new room.
        public static int CountOverlap(IReadOnlyCollection<Vector3Int> oldCells, CleanRoom room)
        {
            var count = 0;
            foreach (var cell in oldCells)
                if (room.Contains(cell)) count++;
            return count;
        }

        // 新部屋の Cells が影響部屋の Cells と完全一致するなら、両方を処理対象から外す（インスタンス維持）。
        // Drop exact-match pairs from processing so the existing instance is preserved.
        public static void RemoveExactMatches(List<CleanRoom> newRooms, HashSet<CleanRoom> affected)
        {
            for (var i = newRooms.Count - 1; i >= 0; i--)
            {
                CleanRoom exact = null;
                foreach (var room in affected)
                    if (RoomEquivalent(room, newRooms[i])) { exact = room; break; }
                if (exact == null) continue;
                affected.Remove(exact);
                newRooms.RemoveAt(i);
            }
        }

        // Cells・Volume・SurfaceArea が全一致なら同一部屋とみなしインスタンスを維持する。
        // Treat as the same room (preserve instance) only if Cells, Volume, and SurfaceArea all match.
        public static bool RoomEquivalent(CleanRoom a, CleanRoom b)
        {
            if (a.Cells.Count != b.Cells.Count) return false;
            if (a.Volume != b.Volume || a.SurfaceArea != b.SurfaceArea) return false;
            foreach (var cell in b.Cells)
                if (!a.Contains(cell)) return false;
            return true;
        }

        public static bool ContainsAny(CleanRoom room, HashSet<Vector3Int> cells)
        {
            foreach (var cell in cells)
                if (room.Contains(cell)) return true;
            return false;
        }
    }
}
