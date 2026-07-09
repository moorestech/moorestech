using System.Collections.Generic;
using System.Linq;
using Core.Master;
using UnityEngine;

namespace Game.CleanRoom.Save
{
    public static class CleanRoomSaveRestore
    {
        public static List<CleanRoomSaveData> BuildSaveData(IReadOnlyList<CleanRoom> rooms)
        {
            // 部屋の揮発IDではなく、セル集合と閾値クラス名で保存する
            // Save by cell set and threshold class name instead of volatile room IDs
            return rooms.Select(room => new CleanRoomSaveData
            {
                ImpurityCount = room.ImpurityCount,
                ClassName = room.ThresholdIndex < MasterHolder.CleanRoomMaster.Thresholds.Count ? MasterHolder.CleanRoomMaster.Thresholds[room.ThresholdIndex].ClassName : null,
                Cells = room.Cells.Select(cell => new[] { cell.x, cell.y, cell.z }).ToList(),
            }).ToList();
        }

        public static void Restore(List<CleanRoomSaveData> saveData, IReadOnlyList<CleanRoom> rooms)
        {
            if (saveData == null) return;

            // 保存された部屋を現行検出結果へ重なり数で対応付ける
            // Match saved rooms to current detection results by overlap count
            var selections = new Dictionary<CleanRoom, RestoreSelection>();
            foreach (var record in saveData)
            {
                var room = FindBestOverlapRoom(record, out var overlap);
                if (room == null || overlap == 0) continue;

                // 同じ現行部屋へ集約された保存レコードは不純物を合算する
                // Sum impurity for all saved records that map to the same current room
                if (!selections.TryGetValue(room, out var selection))
                {
                    selections[room] = new RestoreSelection(record.ImpurityCount, record.ClassName, overlap);
                    continue;
                }

                selection.ImpurityCount += record.ImpurityCount;
                if (overlap > selection.BestOverlap)
                {
                    selection.ClassName = record.ClassName;
                    selection.BestOverlap = overlap;
                }
            }

            // 対応付いた部屋だけ保存値で上書きし、未対応の部屋は初期状態を保つ
            // Overwrite only matched rooms; unmatched rooms keep their freshly detected defaults
            foreach (var selection in selections)
            {
                selection.Key.SetImpurity(selection.Value.ImpurityCount);
                var thresholdIndex = ResolveThresholdIndex(selection.Value.ClassName);
                selection.Key.SetThresholdIndex(thresholdIndex);
            }

            #region Internal

            CleanRoom FindBestOverlapRoom(CleanRoomSaveData record, out int bestOverlap)
            {
                bestOverlap = 0;
                CleanRoom bestRoom = null;
                foreach (var room in rooms)
                {
                    var overlap = 0;
                    foreach (var cell in record.Cells)
                    {
                        if (room.Contains(new Vector3Int(cell[0], cell[1], cell[2]))) overlap++;
                    }

                    if (overlap <= bestOverlap) continue;
                    bestOverlap = overlap;
                    bestRoom = room;
                }

                return bestRoom;
            }

            int ResolveThresholdIndex(string className)
            {
                if (className != null && MasterHolder.CleanRoomMaster.TryGetThresholdIndexByClassName(className, out var index)) return index;
                return MasterHolder.CleanRoomMaster.OutThresholdIndex;
            }

            #endregion
        }

        private class RestoreSelection
        {
            public double ImpurityCount;
            public string ClassName;
            public int BestOverlap;

            public RestoreSelection(double impurityCount, string className, int bestOverlap)
            {
                ImpurityCount = impurityCount;
                ClassName = className;
                BestOverlap = bestOverlap;
            }
        }
    }
}
