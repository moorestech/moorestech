using System;
using Newtonsoft.Json.Linq;

namespace Client.Game.InGame.Playtest.Progress
{
    // 1種別につき「組む」と「読む」を隣に置く。キー文字列が生成側と消費側へ二重定義されると片側改名が無音で集計を落とす
    // Builder and reader sit together per type; duplicating the key strings across producer and consumer lets a one-sided rename drop the aggregate in silence
    internal static class ProgressEvents
    {
        private const string CountKey = "count";
        private const string StateKey = "state";
        private const string NextStateKey = "nextState";
        private const string ChallengeGuidKey = "challengeGuid";
        private const string ResearchGuidKey = "researchGuid";
        private const string RecipeGuidKey = "recipeGuid";
        private const string KindKey = "kind";

        // 設置は1件ずつ残さず区間の合計だけを載せる（ADR 0060 裁定9）
        // Placements are carried as an interval total instead of one line each (ADR 0060 adjudication 9)
        public static ProgressEventEntry BlockPlaced(DateTime utc, ulong tick, int count)
        {
            return ProgressEventEntry.Create(utc, tick, ProgressEventType.BlockPlaced, new JObject { [CountKey] = count });
        }

        public static bool TryReadPlacedCount(ProgressEventEntry entry, out int count)
        {
            count = 0;
            var token = entry.Data[CountKey];
            if (token == null || token.Type != JTokenType.Integer) return false;
            count = (int)token;
            return true;
        }

        public static ProgressEventEntry UiStateChanged(DateTime utc, ulong tick, string state)
        {
            return ProgressEventEntry.Create(utc, tick, ProgressEventType.UiStateChanged, new JObject { [StateKey] = state });
        }

        public static string ReadUiState(ProgressEventEntry entry)
        {
            return (string)entry.Data[StateKey];
        }

        // 離脱は後段の合成なので時刻を持たない。離脱を起こした遷移そのものの時刻とtickをそのまま引き継ぐ
        // A cancel is synthesized afterwards and owns no clock, so it inherits the very transition that caused it
        public static ProgressEventEntry BuildModeCancelledAt(ProgressEventEntry transition, string nextState)
        {
            return new ProgressEventEntry
            {
                T = transition.T,
                Tick = transition.Tick,
                Type = ProgressEventType.BuildModeCancelled,
                Data = new JObject { [NextStateKey] = nextState },
            };
        }

        public static ProgressEventEntry ChallengeCompleted(DateTime utc, ulong tick, string challengeGuid)
        {
            return ProgressEventEntry.Create(utc, tick, ProgressEventType.ChallengeCompleted, new JObject { [ChallengeGuidKey] = challengeGuid });
        }

        public static string ReadChallengeGuid(ProgressEventEntry entry)
        {
            return (string)entry.Data[ChallengeGuidKey];
        }

        public static ProgressEventEntry ResearchCompleted(DateTime utc, ulong tick, string researchGuid)
        {
            return ProgressEventEntry.Create(utc, tick, ProgressEventType.ResearchCompleted, new JObject { [ResearchGuidKey] = researchGuid });
        }

        public static string ReadResearchGuid(ProgressEventEntry entry)
        {
            return (string)entry.Data[ResearchGuidKey];
        }

        public static ProgressEventEntry CraftRequested(DateTime utc, ulong tick, Guid recipeGuid)
        {
            return ProgressEventEntry.Create(utc, tick, ProgressEventType.CraftRequested, new JObject { [RecipeGuidKey] = recipeGuid.ToString() });
        }

        public static string ReadCraftRecipeGuid(ProgressEventEntry entry)
        {
            return (string)entry.Data[RecipeGuidKey];
        }

        public static ProgressEventEntry ReportSent(DateTime utc, ulong tick, string kind)
        {
            return ProgressEventEntry.Create(utc, tick, ProgressEventType.ReportSent, new JObject { [KindKey] = kind });
        }

        public static string ReadReportKind(ProgressEventEntry entry)
        {
            return (string)entry.Data[KindKey];
        }
    }
}
