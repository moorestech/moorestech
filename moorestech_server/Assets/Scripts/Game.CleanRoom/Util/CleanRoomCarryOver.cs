using System.Collections.Generic;

namespace Game.CleanRoom.Util
{
    /// <summary>
    ///     再検出で得た新部屋群へ旧部屋群の不純物と閾値行を引き継ぐ
    ///     Carries impurity and threshold row from old rooms into newly detected rooms
    /// </summary>
    public static class CleanRoomCarryOver
    {
        public static void Apply(List<CleanRoom> newRooms, IReadOnlyList<CleanRoom> oldRooms)
        {
            foreach (var newRoom in newRooms)
            {
                // 重なる旧部屋ごとに N をセル重なり比で按分合算する
                // Sum N from each overlapping old room proportionally to cell overlap
                var carriedImpurity = 0.0;
                CleanRoom maxOverlapRoom = null;
                var maxOverlap = 0;
                foreach (var oldRoom in oldRooms)
                {
                    var overlap = CountOverlap(newRoom, oldRoom);
                    if (overlap == 0) continue;

                    carriedImpurity += oldRoom.ImpurityCount * overlap / oldRoom.Cells.Count;
                    if (overlap <= maxOverlap) continue;
                    maxOverlap = overlap;
                    maxOverlapRoom = oldRoom;
                }

                // 重なる旧部屋が無ければ初期状態（N=0・行=Out）のまま
                // With no overlapping old room, keep the fresh state (N=0, row=Out)
                if (maxOverlapRoom == null) continue;

                newRoom.SetImpurity(carriedImpurity);
                newRoom.SetThresholdIndex(maxOverlapRoom.ThresholdIndex);
            }

            #region Internal

            int CountOverlap(CleanRoom newRoom, CleanRoom oldRoom)
            {
                var count = 0;
                foreach (var cell in newRoom.Cells)
                    if (oldRoom.Contains(cell))
                        count++;
                return count;
            }

            #endregion
        }
    }
}
