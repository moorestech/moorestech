using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.Playtest.Progress.Record.Events;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress.Record
{
    // イベント列から集計値を導く。種別ごとの足し方は各イベントの ApplyTo が持ち、ここは足し先だけを持つ
    // Derives the aggregates from the event list; each event's ApplyTo owns how it adds, and this class only owns where it adds to
    // baseline と event 由来の重複判定を1つの集合で行い、二重計上を構造的に防ぐ
    // One set decides duplicates for both baseline and event origins, so double counting cannot happen
    internal sealed class ProgressRecordAggregate
    {
        public readonly List<string> ReachedChallenges;
        public readonly List<string> CompletedResearch;
        public readonly List<MissingItem> Missing = new();

        private readonly HashSet<string> _reachedChallengeSet;
        private readonly HashSet<string> _completedResearchSet;
        private int _uiStateChangeCount;

        public int PlacedBlockCount { get; private set; }
        public int CraftCount { get; private set; }

        // UI状態が1件も届かなければ null。空文字は実在の状態名と見分けられない
        // Null when no UI state arrived at all; an empty string cannot be told apart from a real state name
        public string LastUiState { get; private set; }

        private ProgressRecordAggregate(ProgressRecordHeader header)
        {
            ReachedChallenges = new List<string>(header.BaselineChallenges);
            CompletedResearch = new List<string>(header.BaselineResearch);
            _reachedChallengeSet = new HashSet<string>(ReachedChallenges);
            _completedResearchSet = new HashSet<string>(CompletedResearch);
        }

        public static ProgressRecordAggregate From(ProgressRecordHeader header, IReadOnlyList<IProgressEvent> events)
        {
            var aggregate = new ProgressRecordAggregate(header);
            foreach (var progressEvent in events) progressEvent.ApplyTo(aggregate);

            // UI状態は1件も届かないことがある（遷移前に終了した・購読が張られていない）。欠損として表明する
            // No UI state may arrive at all (an exit before the first transition, or a subscription that never attached); it is declared as a gap
            if (aggregate._uiStateChangeCount == 0) aggregate.AddMissing("lastUiState", "UI状態の遷移イベントが1件も記録されていない");
            return aggregate;
        }

        public void AddPlacedBlocks(int count)
        {
            PlacedBlockCount += count;
        }

        public void CountCraft()
        {
            CraftCount++;
        }

        public void AddReachedChallenge(string challengeGuid)
        {
            AddDistinct(ReachedChallenges, _reachedChallengeSet, challengeGuid);
        }

        public void AddCompletedResearch(string researchGuid)
        {
            AddDistinct(CompletedResearch, _completedResearchSet, researchGuid);
        }

        public void SetLastUiState(string state)
        {
            _uiStateChangeCount++;
            LastUiState = state;
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
