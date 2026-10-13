using System.Collections.Generic;
using Game.Train.RailGraph;

namespace Game.Train.Diagram
{
    // ノード削除後の時刻表とカーソル位置を整える
    // Adjust the timetable and cursor after a rail node is removed
    internal static class TrainDiagramNodeRemoval
    {
        internal static int Remove(
            List<TrainDiagramEntry> entries, int currentIndex, IRailNode removedNode,
            out bool removedAny, out bool currentRemoved)
        {
            removedAny = false;
            currentRemoved = false;

            // 末尾から削除し、現在地より前の削除分だけインデックスを詰める
            // Remove from the end and shift the cursor for preceding entries
            for (var i = entries.Count - 1; 0 <= i; i--)
            {
                if (!entries[i].MatchesNode(removedNode))
                {
                    continue;
                }

                removedAny = true;
                if ((0 <= currentIndex) && (i < currentIndex))
                {
                    currentIndex--;
                }
                else if ((0 <= currentIndex) && (i == currentIndex))
                {
                    currentRemoved = true;
                }
                entries.RemoveAt(i);
            }

            return entries.Count == 0 ? -1 : currentIndex % entries.Count;
        }
    }
}
