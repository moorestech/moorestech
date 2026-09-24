using System.Collections.Generic;
using System.Linq;
using Game.Train.RailGraph;
using Game.Train.Unit;

namespace Game.Train.Diagram
{
    // 既存の時刻表セーブ形式と実行時エントリを相互変換する
    // Convert between the existing timetable save format and runtime entries
    public static class TrainDiagramSaveDataConverter
    {
        public static TrainDiagramSaveData Create(TrainDiagram diagram)
        {
            var entries = new List<TrainDiagramEntrySaveData>();
            foreach (var entry in diagram.Entries)
            {
                entries.Add(new TrainDiagramEntrySaveData
                {
                    EntryId = entry.entryId,
                    Node = entry.Node.ConnectionDestination,
                    DepartureConditions = entry.DepartureConditionTypes?.ToList() ??
                                          new List<TrainDiagram.DepartureConditionType>(),
                    WaitForTicksInitial = entry.GetWaitForTicksInitialTicks(),
                    WaitForTicksRemaining = entry.GetWaitForTicksRemainingTicks()
                });
            }

            return new TrainDiagramSaveData
            {
                CurrentIndex = diagram.CurrentIndex,
                Entries = entries
            };
        }

        public static void Restore(
            TrainDiagram diagram, TrainDiagramSaveData saveData, IRailGraphProvider railGraphProvider)
        {
            var entries = new List<TrainDiagramEntry>();
            if (saveData?.Entries == null)
            {
                diagram.SetRestoredEntries(entries, -1);
                return;
            }

            // 既存セーブのノード解決規則を維持する
            // Preserve the node resolution rules for existing saves
            foreach (var entryData in saveData.Entries)
            {
                if (entryData == null)
                {
                    continue;
                }

                var node = railGraphProvider.ResolveRailNode(entryData.Node);
                if (node == null)
                {
                    continue;
                }

                entries.Add(TrainDiagramEntry.CreateFromSaveData(
                    node, entryData.EntryId, entryData.DepartureConditions,
                    entryData.WaitForTicksInitial, entryData.WaitForTicksRemaining));
            }

            if (entries.Count == 0)
            {
                diagram.SetRestoredEntries(entries, -1);
                return;
            }

            var restoredIndex = saveData.CurrentIndex;
            if (restoredIndex < -1)
            {
                restoredIndex = -1;
            }
            else if (restoredIndex >= entries.Count)
            {
                restoredIndex = entries.Count - 1;
            }

            diagram.SetRestoredEntries(entries, restoredIndex);
        }
    }
}
