using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress
{
    // イベント列から集計値を導く。baseline と event 由来の重複判定を1つの集合で行い、二重計上を構造的に防ぐ
    // Derives the aggregates from the event list; one set decides duplicates for both baseline and event origins, so double counting cannot happen
    internal sealed class ProgressRecordAggregate
    {
        public List<string> ReachedChallenges;
        public List<string> CompletedResearch;
        public int PlacedBlockCount;
        public int CraftCount;
        public string LastUiState = "";
        public readonly List<MissingItem> Missing = new();

        public static ProgressRecordAggregate From(ProgressRecordHeader header, IReadOnlyList<ProgressEventEntry> events)
        {
            var aggregate = new ProgressRecordAggregate
            {
                ReachedChallenges = new List<string>(header.BaselineChallenges),
                CompletedResearch = new List<string>(header.BaselineResearch),
            };
            var reachedChallengeSet = new HashSet<string>(aggregate.ReachedChallenges);
            var completedResearchSet = new HashSet<string>(aggregate.CompletedResearch);
            var uiStateChangeCount = 0;

            foreach (var entry in events)
                switch (entry.Type)
                {
                    case ProgressEventType.BlockPlaced:
                        aggregate.AddPlacedBlocks(entry);
                        break;
                    case ProgressEventType.CraftCompleted:
                        aggregate.CraftCount++;
                        break;
                    case ProgressEventType.ChallengeCompleted:
                        AddDistinct(aggregate.ReachedChallenges, reachedChallengeSet, ProgressEvents.ReadChallengeGuid(entry));
                        break;
                    case ProgressEventType.ResearchCompleted:
                        AddDistinct(aggregate.CompletedResearch, completedResearchSet, ProgressEvents.ReadResearchGuid(entry));
                        break;
                    case ProgressEventType.UiStateChanged:
                        uiStateChangeCount++;
                        aggregate.LastUiState = ProgressEvents.ReadUiState(entry) ?? aggregate.LastUiState;
                        break;
                }

            // UI状態は1件も届かないことがある（遷移前に終了した・購読が張られていない）。空文字のまま出すと「GameScreenで離脱」と区別できない
            // No UI state may arrive at all (an exit before the first transition, or a subscription that never attached); an empty string alone is indistinguishable from a real state
            if (uiStateChangeCount == 0) aggregate.AddMissing("lastUiState", "UI状態の遷移イベントが1件も記録されていない");
            return aggregate;
        }

        private void AddPlacedBlocks(ProgressEventEntry entry)
        {
            if (!ProgressEvents.TryReadPlacedCount(entry, out var count))
            {
                AddMissing(ProgressEventType.BlockPlaced, $"設置数の入っていない集約イベントを読み飛ばした t:{entry.T}");
                return;
            }
            PlacedBlockCount += count;
        }

        private void AddMissing(string item, string reason)
        {
            Debug.LogWarning($"進行記録の集計で欠損 {item}: {reason}");
            Missing.Add(new MissingItem { Item = item, Reason = reason });
        }

        private static void AddDistinct(List<string> values, HashSet<string> seen, string value)
        {
            if (string.IsNullOrEmpty(value) || !seen.Add(value)) return;
            values.Add(value);
        }
    }
}
