using System.Collections.Generic;
using Game.CleanRoom.SaveLoad;
using UnityEngine;

namespace Game.CleanRoom
{
    // CleanRoomWorld と保存レコード（CleanRoomSaveData）の相互変換。検出済み部屋＋Degraded孤立を保存し、最大重なりで復元する。
    // Converts between CleanRoomWorld and save records; saves detected rooms + Degraded orphans, restores by max cell overlap.
    public static class CleanRoomSavePersistence
    {
        // 検出中の全部屋＋Degraded孤立を保存する（Invalid孤立は保存しない）。
        // Save all detected rooms plus Degraded orphans (Invalid orphans are not saved).
        public static List<CleanRoomSaveData> GetSaveData(CleanRoomWorld world)
        {
            var result = new List<CleanRoomSaveData>();
            foreach (var room in world.Rooms) result.Add(ToSaveData(room));
            foreach (var orphan in world.Orphans)
                if (orphan.Status == CleanRoomRoomStatus.Degraded) result.Add(ToSaveData(orphan));
            return result;
        }

        // 再検出済みの部屋へ最大セル重なりで照合して復元する。複数レコード同部屋は N 合算。
        // Restore by max cell overlap; multiple records on one room sum their N.
        public static void Restore(CleanRoomWorld world, IReadOnlyList<CleanRoomSaveData> saveData)
        {
            if (saveData == null) return;

            // 部屋ごとの最大重なりレコードを記録しつつ N を合算する。
            // Track the max-overlap record per room while summing N.
            var bestByRoom = new Dictionary<CleanRoom, (int overlap, CleanRoomSaveData record)>();

            foreach (var record in saveData)
            {
                if (record?.Cells == null) continue;
                var recordCells = ParseCells(record.Cells);

                CleanRoom best = null;
                var bestOverlap = 0;
                foreach (var room in world.Rooms)
                {
                    var overlap = 0;
                    foreach (var cell in recordCells)
                        if (room.Contains(cell)) overlap++;
                    if (overlap > bestOverlap) { bestOverlap = overlap; best = room; }
                }

                if (best == null)
                {
                    // 未マッチ: Degraded レコードだけ孤立状態として復元（猶予継続）。他は破棄。
                    // Unmatched: only Degraded records become orphans (grace keeps running).
                    if ((CleanRoomRoomStatus)record.Status == CleanRoomRoomStatus.Degraded)
                        world.Orphans.Add(CreateOrphanFromRecord(world, record, recordCells));
                    continue;
                }

                best.AddImpurity(record.ImpurityCount); // 合算（後勝ち上書き禁止）
                if (!bestByRoom.TryGetValue(best, out var current) || bestOverlap > current.overlap)
                    bestByRoom[best] = (bestOverlap, record);
            }

            // 行・状態・猶予は最大重なりレコードを採用。
            // Threshold row / status / grace come from the max-overlap record.
            foreach (var kvp in bestByRoom)
            {
                kvp.Key.SetThresholdIndex(kvp.Value.record.ThresholdIndex);
                kvp.Key.SetStatus((CleanRoomRoomStatus)kvp.Value.record.Status, kvp.Value.record.GraceRemainingSeconds);
            }
        }

        // CleanRoom → CleanRoomSaveData へ変換する。
        // Convert a CleanRoom to its save record.
        private static CleanRoomSaveData ToSaveData(CleanRoom room)
        {
            var cells = new List<int[]>();
            foreach (var c in room.Cells)
                cells.Add(new[] { c.x, c.y, c.z });
            return new CleanRoomSaveData
            {
                ImpurityCount = room.ImpurityCount,
                ThresholdIndex = room.ThresholdIndex,
                Status = (int)room.Status,
                GraceRemainingSeconds = (float)room.GraceRemainingSeconds,
                Cells = cells,
            };
        }

        // セルリスト（int[]）を HashSet<Vector3Int> へ変換する。
        // Convert the cell list (int[]) back to a HashSet<Vector3Int>.
        private static HashSet<Vector3Int> ParseCells(List<int[]> cells)
        {
            var set = new HashSet<Vector3Int>(cells.Count);
            foreach (var c in cells)
                if (c != null && c.Length >= 3) set.Add(new Vector3Int(c[0], c[1], c[2]));
            return set;
        }

        // 未マッチ Degraded レコードから孤立 CleanRoom を生成する（猶予継続用）。
        // 孤立中は純度tick対象外なので V/S=0 で良い。再封時に検出が正値で作り直す。
        // Create an orphan CleanRoom from an unmatched Degraded record (Volume=cells / SurfaceArea=0 is fine; orphans are not purity-ticked).
        private static CleanRoom CreateOrphanFromRecord(CleanRoomWorld world, CleanRoomSaveData record, HashSet<Vector3Int> recordCells)
        {
            var orphan = new CleanRoom(world.AllocateRoomId(), recordCells, recordCells.Count, 0);
            orphan.AddImpurity(record.ImpurityCount);
            orphan.SetThresholdIndex(record.ThresholdIndex);
            orphan.SetStatus(CleanRoomRoomStatus.Degraded, record.GraceRemainingSeconds);
            return orphan;
        }
    }
}
