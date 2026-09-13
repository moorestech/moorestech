using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Client.Game.InGame.Playtest.Progress
{
    // ヘッダとイベント列から record.json を組む純関数。集計もキャンセル合成もここだけで行う（shared-contracts §3）
    // A pure function composing record.json from the header and events; all aggregation and cancel synthesis live here (shared-contracts §3)
    public static class ProgressRecordComposer
    {
        public const string PlaceBlockStateName = "PlaceBlock";

        public static string Compose(ProgressRecordHeader header, IReadOnlyList<ProgressEventEntry> events, string endReason, DateTime sessionEndUtc)
        {
            var enriched = WithSynthesizedBuildModeCancel(events);
            var sessionStart = ParseUtc(header.SessionStart, sessionEndUtc);
            var playSeconds = Math.Max(0, (sessionEndUtc - sessionStart).TotalSeconds);

            var reachedChallenges = new List<string>(header.BaselineChallenges);
            var completedResearch = new List<string>(header.BaselineResearch);
            var placedBlockCount = 0;
            var craftCount = 0;
            var lastUiState = "";
            var eventArray = new JArray();

            foreach (var entry in enriched)
            {
                eventArray.Add(entry.ToJObject());
                switch (entry.Type)
                {
                    case ProgressEventType.BlockPlaced:
                        placedBlockCount++;
                        break;
                    case ProgressEventType.CraftExecuted:
                        craftCount++;
                        break;
                    case ProgressEventType.ChallengeCompleted:
                        AddDistinct(reachedChallenges, (string)entry.Data["challengeGuid"]);
                        break;
                    case ProgressEventType.ResearchCompleted:
                        AddDistinct(completedResearch, (string)entry.Data["researchGuid"]);
                        break;
                    case ProgressEventType.UiStateChanged:
                        lastUiState = (string)entry.Data["state"] ?? lastUiState;
                        break;
                }
            }

            var record = new JObject
            {
                ["schemaVersion"] = header.SchemaVersion,
                ["steamId"] = header.SteamId ?? "",
                ["buildInfo"] = header.BuildInfo == null ? JValue.CreateNull() : JObject.Parse(JsonConvert.SerializeObject(header.BuildInfo, new JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() })),
                ["sessionStart"] = header.SessionStart,
                ["sessionEnd"] = sessionEndUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                ["endReason"] = endReason,
                ["playSeconds"] = playSeconds,
                ["worldCreatedAt"] = header.WorldCreatedAt ?? "",
                ["totalPlaySeconds"] = header.TotalPlaySecondsAtStart + playSeconds,
                ["reachedChallenges"] = new JArray(reachedChallenges),
                ["completedResearch"] = new JArray(completedResearch),
                ["placedBlockCount"] = placedBlockCount,
                ["craftCount"] = craftCount,
                ["lastUiState"] = lastUiState,
                ["events"] = eventArray,
            };
            return record.ToString(Formatting.Indented);
        }

        // 「PlaceBlock に入って、1件も設置しないまま抜けた」滞在だけを離脱として合成する
        // Synthesizes a cancel only for a stay that entered PlaceBlock and left without a single placement
        public static List<ProgressEventEntry> WithSynthesizedBuildModeCancel(IReadOnlyList<ProgressEventEntry> events)
        {
            var result = new List<ProgressEventEntry>(events.Count + 4);
            var insideBuildMode = false;
            var placedInsideBuildMode = false;

            foreach (var entry in events)
            {
                if (insideBuildMode && entry.Type == ProgressEventType.BlockPlaced) placedInsideBuildMode = true;

                if (entry.Type == ProgressEventType.UiStateChanged)
                {
                    var state = (string)entry.Data["state"];
                    var entering = state == PlaceBlockStateName;
                    if (insideBuildMode && !entering && !placedInsideBuildMode)
                    {
                        result.Add(ProgressEventEntry.Create(ParseUtc(entry.T, DateTime.UtcNow), entry.Tick, ProgressEventType.BuildModeCancelled, new JObject { ["nextState"] = state }));
                    }
                    if (!insideBuildMode && entering) placedInsideBuildMode = false;
                    insideBuildMode = entering;
                }

                result.Add(entry);
            }
            return result;
        }

        private static void AddDistinct(List<string> values, string value)
        {
            if (string.IsNullOrEmpty(value) || values.Contains(value)) return;
            values.Add(value);
        }

        private static DateTime ParseUtc(string iso, DateTime fallback)
        {
            return DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : fallback;
        }
    }
}
