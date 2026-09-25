using System.Collections.Generic;
using System.Linq;
using Game.Train.RailGraph;
using Game.Train.Unit;
using UnityEngine;

namespace Game.Train.Diagram
{
    // 既存の時刻表セーブ形式と実行時エントリを相互変換する
    // Convert between the existing timetable save format and runtime entries
    internal static class TrainDiagramSaveDataConverter
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
            var droppedEntryCount = 0;
            for (var entryIndex = 0; entryIndex < saveData.Entries.Count; entryIndex++)
            {
                var entryData = saveData.Entries[entryIndex];
                if (entryData == null)
                {
                    // 壊れたセーブ項目は復元できないので落とす。無音にせず位置と件数を残す
                    // A broken save entry cannot be restored, so drop it while logging its position and count
                    droppedEntryCount++;
                    Debug.LogWarning($"[TrainDiagramRestore] entryがnullのため復元できず落とした index={entryIndex} entries={saveData.Entries.Count} currentIndex={saveData.CurrentIndex}");
                    continue;
                }

                var node = railGraphProvider.ResolveRailNode(entryData.Node);
                if (node == null)
                {
                    // セーブ時のレールが現存しないentryは復元できない
                    // An entry whose rail no longer exists cannot be restored
                    droppedEntryCount++;
                    Debug.LogWarning($"[TrainDiagramRestore] 保存されたnodeが現存せず落とした index={entryIndex} entryId={entryData.EntryId} entries={saveData.Entries.Count} currentIndex={saveData.CurrentIndex}");
                    continue;
                }

                entries.Add(TrainDiagramEntry.CreateFromSaveData(
                    node, entryData.EntryId, entryData.DepartureConditions,
                    entryData.WaitForTicksInitial, entryData.WaitForTicksRemaining));
            }

            if (entries.Count == 0)
            {
                if (0 < droppedEntryCount)
                {
                    Debug.LogWarning($"[TrainDiagramRestore] 全entryが復元できず時刻表が空になった dropped={droppedEntryCount} currentIndex={saveData.CurrentIndex}");
                }
                diagram.SetRestoredEntries(entries, -1);
                return;
            }

            // 落としたentryの分だけ現在地がずれるため、範囲外なら末尾へ丸めて理由を残す
            // Dropped entries shift the cursor, so an out-of-range index is clamped to the tail with a logged reason
            var restoredIndex = saveData.CurrentIndex;
            if (restoredIndex < -1)
            {
                Debug.LogWarning($"[TrainDiagramRestore] 保存されたcurrentIndexが負すぎるため現在地なしへ丸めた currentIndex={saveData.CurrentIndex} dropped={droppedEntryCount} entries={entries.Count}");
                restoredIndex = -1;
            }
            else if (entries.Count <= restoredIndex)
            {
                Debug.LogWarning($"[TrainDiagramRestore] 保存されたcurrentIndexが範囲外のため末尾へ丸めた currentIndex={saveData.CurrentIndex} dropped={droppedEntryCount} entries={entries.Count}");
                restoredIndex = entries.Count - 1;
            }

            diagram.SetRestoredEntries(entries, restoredIndex);
        }
    }
}
