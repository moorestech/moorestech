using System.Collections.Generic;
using Game.Train.RailGraph;

namespace Game.Train.Diagram
{
    // 時刻表エントリの追加と出発条件の評価をまとめる
    // Handle entry insertion and departure condition evaluation
    internal static class TrainDiagramEntryOperations
    {
        internal static TrainDiagramEntry Add(List<TrainDiagramEntry> entries, ref int currentIndex, IRailNode node)
        {
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var entry = new TrainDiagramEntry(node);
            entries.Add(entry);
            return entry;
        }

        internal static TrainDiagramEntry Add(
            List<TrainDiagramEntry> entries, ref int currentIndex, IRailNode node,
            TrainDiagram.DepartureConditionType departureConditionType, int waitTicks)
        {
            var entry = Add(entries, ref currentIndex, node);
            if (departureConditionType == TrainDiagram.DepartureConditionType.WaitForTicks)
            {
                entry.SetDepartureWaitTicks(waitTicks);
            }
            else
            {
                entry.SetDepartureCondition(departureConditionType);
            }
            return entry;
        }

        internal static TrainDiagramEntry Insert(
            List<TrainDiagramEntry> entries, ref int currentIndex, int index, IRailNode node)
        {
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            if (index < 0)
            {
                index = 0;
            }
            else if (index > entries.Count)
            {
                index = entries.Count;
            }

            var entry = new TrainDiagramEntry(node);
            entries.Insert(index, entry);
            return entry;
        }

        internal static void Tick(List<TrainDiagramEntry> entries, ref int currentIndex, ITrainDiagramContext context)
        {
            if (currentIndex < 0)
            {
                return;
            }

            if (currentIndex >= entries.Count)
            {
                currentIndex = -1;
                return;
            }

            entries[currentIndex].Tick(context);
        }

        internal static bool CanDepart(List<TrainDiagramEntry> entries, ref int currentIndex, ITrainDiagramContext context)
        {
            if (currentIndex < 0)
            {
                return true;
            }

            if (currentIndex >= entries.Count)
            {
                currentIndex = -1;
                return true;
            }

            return entries[currentIndex].CanDepart(context);
        }
    }
}
