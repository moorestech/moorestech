using System.Collections.Generic;
using Game.Block.Interface;
using UnityEngine;

namespace Game.CleanRoom
{
    // 検出済み部屋と孤立部屋の集合＋ID採番を保持し、セル帰属クエリを提供する状態オブジェクト。
    // 各サービス（検出/純度/保存）はこの world を共有して読み書きする。
    // Holds the detected/orphan room sets plus id allocation, and answers cell-membership queries.
    // The detection / purity / save services all share and mutate this world.
    public class CleanRoomWorld
    {
        public IReadOnlyList<CleanRoom> Rooms => _rooms;
        public List<CleanRoom> Orphans => _orphans;

        private List<CleanRoom> _rooms = new();
        private readonly List<CleanRoom> _orphans = new();

        // 次に生成する部屋の Id。差分更新でも重複しないよう単調増加。
        // Monotonic next room id so incremental detection never reuses an id.
        private int _nextRoomId;

        public void ReplaceRooms(List<CleanRoom> rooms)
        {
            _rooms = rooms;
        }

        // 検出結果に単調増加 Id を振り直す（Detector は 0 起点のため）。
        // Reassign monotonic ids to detected rooms (the detector starts from 0).
        public void ReassignRoomIds(List<CleanRoom> rooms)
        {
            foreach (var room in rooms) room.SetId(_nextRoomId++);
        }

        public int AllocateRoomId()
        {
            return _nextRoomId++;
        }

        public bool TryGetCleanRoomAt(Vector3Int cell, out CleanRoom room)
        {
            foreach (var r in _rooms)
                if (r.Contains(cell)) { room = r; return true; }
            room = null;
            return false;
        }

        // ブロックの全占有セルが同一部屋の Cells に含まれるとき true。内部ブロック（機械等）の帰属判定用。
        // 境界ブロックのセルは Cells に含まれないため常に false（境界用は GetAdjacentCleanRooms）。
        // True iff every occupied cell lies in the SAME room's Cells. Boundary blocks always return false.
        public bool TryGetCleanRoom(IBlock block, out CleanRoom room)
        {
            var info = block.BlockPositionInfo;
            CleanRoom found = null;
            for (var x = info.MinPos.x; x <= info.MaxPos.x; x++)
            for (var y = info.MinPos.y; y <= info.MaxPos.y; y++)
            for (var z = info.MinPos.z; z <= info.MaxPos.z; z++)
            {
                if (!TryGetCleanRoomAt(new Vector3Int(x, y, z), out var r)) { room = null; return false; }
                if (found == null) found = r;
                else if (!ReferenceEquals(found, r)) { room = null; return false; } // 別部屋にまたがる / spans two rooms
            }
            room = found;
            return found != null;
        }

        // 境界ブロックの占有セルの6近傍を部屋セルマップに照合し、面する部屋を重複なしで返す。
        // 境界セル自体は Cells に属さないため部屋内クエリでは引けない。共有境界では複数返り得る（0.5）。
        // Resolve the rooms facing a boundary block via its occupied cells' 6-neighbors (deduplicated). May return multiple (§0.5).
        public IReadOnlyList<CleanRoom> GetAdjacentCleanRooms(IBlock boundaryBlock)
        {
            var result = new List<CleanRoom>();
            var info = boundaryBlock.BlockPositionInfo;
            for (var x = info.MinPos.x; x <= info.MaxPos.x; x++)
            for (var y = info.MinPos.y; y <= info.MaxPos.y; y++)
            for (var z = info.MinPos.z; z <= info.MaxPos.z; z++)
            foreach (var n in CleanRoomCellSets.SixNeighbors(new Vector3Int(x, y, z)))
            {
                if (!TryGetCleanRoomAt(n, out var room)) continue;
                if (!result.Contains(room)) result.Add(room);
            }
            return result;
        }
    }
}
