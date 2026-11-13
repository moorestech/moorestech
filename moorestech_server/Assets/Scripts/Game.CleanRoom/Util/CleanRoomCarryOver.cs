using System.Collections.Generic;

namespace Game.CleanRoom.Util
{
    /// <summary>
    ///     再検出で得た新部屋群へ旧部屋群の閾値行を引き継ぐ
    ///     Carries the threshold row from old rooms into newly detected rooms
    /// </summary>
    public static class CleanRoomCarryOver
    {
        public static void Apply(List<CleanRoom> newRooms, IReadOnlyList<CleanRoom> oldRooms)
        {
            foreach (var newRoom in newRooms)
            {
                // 最も多くセルが重なる旧部屋の閾値行を引き継ぐ
                // Carry the threshold row from the old room with the largest cell overlap
                CleanRoom maxOverlapRoom = null;
                var maxOverlap = 0;
                foreach (var oldRoom in oldRooms)
                {
                    var overlap = CountOverlap(newRoom, oldRoom);
                    if (overlap <= maxOverlap) continue;
                    maxOverlap = overlap;
                    maxOverlapRoom = oldRoom;
                }

                // 重なる旧部屋が無ければ初期状態（行=Out）のまま
                // With no overlapping old room, keep the fresh state (row=Out)
                if (maxOverlapRoom == null) continue;
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
