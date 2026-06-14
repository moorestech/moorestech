using System.Collections.Generic;
using UnityEngine;

namespace Game.CleanRoom
{
    // シード周辺だけを局所fillし、影響を受ける既存部屋のみを差分更新する増分検出器。world を直接書き換える。
    // Local incremental detector: floods around a seed and differentially updates only the affected rooms, mutating the world in place.
    public class CleanRoomIncrementalDetector
    {
        private readonly CleanRoomWorld _world;

        public CleanRoomIncrementalDetector(CleanRoomWorld world)
        {
            _world = world;
        }

        // シード周辺を局所 fill し、影響を受ける既存部屋だけを差分更新する。fill 訪問セル数を返す。
        // Locally fill around the seed and differentially update only the affected rooms. Returns visited cells.
        public int DetectAroundSeed(Vector3Int seed, HashSet<Vector3Int> boundaryCells, HashSet<Vector3Int> occupiedCells)
        {
            // 局所 fill で新たな密閉部屋を検出（触れた壁AABB+1・MaxRoomVolume で縛る）。
            // Detect new sealed rooms by local fill (bounded by touched-wall AABB+1 / MaxRoomVolume).
            var seeds = new List<Vector3Int> { seed };
            var newRooms = CleanRoomDetector.DetectFromSeeds(seeds, boundaryCells, occupiedCells, 0, out var visited);

            // 影響対象＝シード近傍に重なる既存部屋 ＋ 新部屋セルに重なる既存部屋。
            // Affected rooms = existing rooms overlapping the seed neighborhood or any new-room cell.
            var probe = ProbeRegion(seed);
            var affected = CollectAffectedRooms(probe, newRooms);

            // この領域の再検出で、重なる Invalid 孤立を破棄する（全走査と同じ「再検出で破棄」を実現）。
            // Invalid はプールに入れない＝引き継がない（新部屋は N=0 開始。汚染の蘇生を防ぐ）。
            // Discard Invalid orphans overlapping this re-detected region (no carry-over -> new room starts at N=0, no impurity resurrection).
            DiscardOverlappingInvalidOrphans(probe, newRooms);

            // 新部屋が既存部屋の Cells と完全一致なら何もしない（インスタンス維持）。
            // If a new room exactly matches an existing room's Cells, keep the instance (do nothing).
            CleanRoomCarryOver.RemoveExactMatches(newRooms, affected);
            if (newRooms.Count == 0 && affected.Count == 0) return visited;

            _world.ReassignRoomIds(newRooms);

            // 影響部屋＋それに重なる Degraded 孤立だけをプールにし、引き継ぎ後に _rooms を部分置換する。
            // Pool = affected rooms + overlapping Degraded orphans only; partially replace rooms after carry-over.
            var pool = new List<CleanRoom>(affected);
            PullOverlappingDegradedOrphans(probe, newRooms, pool);

            CleanRoomCarryOver.ApplyCarryOver(newRooms, pool, _world.Orphans);

            // 影響部屋を取り除き、新部屋を加える（触れていない部屋はインスタンスごと維持）。
            // Drop affected rooms and add the new ones; untouched rooms keep their instances.
            var next = new List<CleanRoom>(_world.Rooms.Count);
            foreach (var room in _world.Rooms)
                if (!affected.Contains(room)) next.Add(room);
            next.AddRange(newRooms);
            _world.ReplaceRooms(next);

            return visited;

            #region Internal

            // シードと6近傍を既存部屋の Cells 重なり判定に使う探索域。
            // Probe region: the seed plus its 6 neighbors, used to find overlapping existing rooms.
            HashSet<Vector3Int> ProbeRegion(Vector3Int s)
            {
                var set = new HashSet<Vector3Int> { s };
                set.Add(new Vector3Int(s.x + 1, s.y, s.z));
                set.Add(new Vector3Int(s.x - 1, s.y, s.z));
                set.Add(new Vector3Int(s.x, s.y + 1, s.z));
                set.Add(new Vector3Int(s.x, s.y - 1, s.z));
                set.Add(new Vector3Int(s.x, s.y, s.z + 1));
                set.Add(new Vector3Int(s.x, s.y, s.z - 1));
                return set;
            }

            // probe セルを含む、または新部屋セルに重なる既存部屋を集める。
            // Collect existing rooms that contain a probe cell or overlap any new-room cell.
            HashSet<CleanRoom> CollectAffectedRooms(HashSet<Vector3Int> probeCells, List<CleanRoom> rooms)
            {
                var affectedRooms = new HashSet<CleanRoom>();
                foreach (var room in _world.Rooms)
                {
                    if (CleanRoomCarryOver.ContainsAny(room, probeCells)) { affectedRooms.Add(room); continue; }
                    foreach (var newRoom in rooms)
                        if (CleanRoomCarryOver.CountOverlap(room.Cells, newRoom) > 0) { affectedRooms.Add(room); break; }
                }
                return affectedRooms;
            }

            // probe または新部屋に重なる Degraded 孤立をプールへ移す（猶予中の再密閉対応）。
            // Move Degraded orphans overlapping the probe or a new room into the pool (reseal within grace).
            void PullOverlappingDegradedOrphans(HashSet<Vector3Int> probeCells, List<CleanRoom> rooms, List<CleanRoom> targetPool)
            {
                var orphans = _world.Orphans;
                for (var i = orphans.Count - 1; i >= 0; i--)
                {
                    var orphan = orphans[i];
                    if (orphan.Status != CleanRoomRoomStatus.Degraded) continue;

                    var overlaps = CleanRoomCarryOver.ContainsAny(orphan, probeCells);
                    if (!overlaps)
                        foreach (var newRoom in rooms)
                            if (CleanRoomCarryOver.CountOverlap(orphan.Cells, newRoom) > 0) { overlaps = true; break; }

                    if (!overlaps) continue;
                    targetPool.Add(orphan);
                    orphans.RemoveAt(i);
                }
            }

            // probe または新部屋に重なる Invalid 孤立を破棄する（引き継がず削除のみ＝N蘇生なし）。
            // Discard Invalid orphans overlapping the probe or a new room (removed only, never pooled -> no N resurrection).
            void DiscardOverlappingInvalidOrphans(HashSet<Vector3Int> probeCells, List<CleanRoom> rooms)
            {
                var orphans = _world.Orphans;
                for (var i = orphans.Count - 1; i >= 0; i--)
                {
                    var orphan = orphans[i];
                    if (orphan.Status != CleanRoomRoomStatus.Invalid) continue;

                    var overlaps = CleanRoomCarryOver.ContainsAny(orphan, probeCells);
                    if (!overlaps)
                        foreach (var newRoom in rooms)
                            if (CleanRoomCarryOver.CountOverlap(orphan.Cells, newRoom) > 0) { overlaps = true; break; }

                    if (!overlaps) continue;
                    orphans.RemoveAt(i);
                }
            }

            #endregion
        }
    }
}
