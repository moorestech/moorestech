using System;
using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.UI.UIState;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress
{
    // ヘッダとイベント列から record.json を組む純関数。集計もキャンセル合成もここだけで行う（shared-contracts §3）
    // A pure function composing record.json from the header and events; all aggregation and cancel synthesis live here (shared-contracts §3)
    internal static class ProgressRecordComposer
    {
        // 建築モードの判定はUI状態のSSOTから引く。文字列を持つと状態名の改名で合成が無音で止まる
        // The build-mode check comes from the UI state SSOT; a literal would silently stop the synthesis when the state is renamed
        private const string PlaceBlockStateName = nameof(UIStateEnum.PlaceBlock);

        public static string Compose(ProgressRecordHeader header, IReadOnlyList<ProgressEventEntry> events, string endReason, DateTime sessionEndUtc)
        {
            var enriched = WithSynthesizedBuildModeCancel(events);
            var missing = new List<MissingItem>(header.Missing);
            var aggregate = ProgressRecordAggregate.From(header, enriched);
            missing.AddRange(aggregate.Missing);

            var playSeconds = ResolvePlaySeconds();
            var totalPlaySeconds = ResolveTotalPlaySeconds();

            // 設置数は区間ごとの集計で、終了flushを通らないcrash-recoveredでは最後のUI遷移以降のぶんが記録に残らない（ADR 0060 裁定9の代償）
            // Placements are aggregated per interval, and crash-recovered never passes the shutdown flush, so whatever followed the last UI transition is gone (the cost of ADR 0060 adjudication 9)
            // 建築モードの跡で絞れない: 設置の出所はサーバーの全体配信で、協力プレイの相方・ブループリント貼り付け・線路敷設は PlaceBlock へ入らずに数を増やす
            // A build-mode trace cannot narrow this: the count comes from the server's broadcast, and a co-op partner, a blueprint paste or a rail run all raise it without entering PlaceBlock
            if (endReason == ProgressEndReason.CrashRecovered) AddMissing("placedBlockCount", "最後のUI状態遷移以降に置かれたブロック数は記録されていない（集計の書き出し前に異常終了した）");

            var eventArray = new JArray();
            foreach (var entry in enriched) eventArray.Add(entry.ToJObject());

            var record = new JObject
            {
                ["schemaVersion"] = header.SchemaVersion,
                ["steamId"] = header.SteamId ?? "",
                ["buildInfo"] = header.BuildInfo == null ? JValue.CreateNull() : JObject.Parse(JsonConvert.SerializeObject(header.BuildInfo, new JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() })),
                ["sessionStart"] = header.SessionStart,
                ["sessionEnd"] = ProgressUtcTime.ToIso(sessionEndUtc),
                ["endReason"] = endReason,
                ["playSeconds"] = playSeconds,
                ["worldCreatedAt"] = header.WorldCreatedAt ?? "",
                ["totalPlaySeconds"] = totalPlaySeconds,
                ["reachedChallenges"] = new JArray(aggregate.ReachedChallenges),
                ["completedResearch"] = new JArray(aggregate.CompletedResearch),
                ["placedBlockCount"] = aggregate.PlacedBlockCount,
                ["craftCount"] = aggregate.CraftRequestCount,
                ["lastUiState"] = aggregate.LastUiState,
                ["missing"] = ToJsonArray(),
                ["events"] = eventArray,
            };
            return record.ToString(Formatting.Indented);

            #region Internal

            // 開始時刻が読めない・終了より後ろ、のどちらも実データと同じ0秒へ潰さず理由を残す
            // Neither an unreadable start nor one later than the end collapses into a plain 0 seconds; the reason is kept
            double ResolvePlaySeconds()
            {
                if (!ProgressUtcTime.TryParseIso(header.SessionStart, out var sessionStart))
                {
                    AddMissing("playSeconds", $"セッション開始時刻を読めないため0秒として記録した value:{header.SessionStart}");
                    return 0;
                }

                var seconds = (sessionEndUtc - sessionStart).TotalSeconds;
                if (0 <= seconds) return seconds;

                AddMissing("playSeconds", $"終了時刻が開始時刻より前のため0秒として記録した start:{header.SessionStart} end:{ProgressUtcTime.ToIso(sessionEndUtc)}");
                return 0;
            }

            // 累計は「取得した瞬間からの経過」だけを足す。セッション開始起点で足すとロードと応答待ちのぶんが二重に乗る
            // Only the span since the value was captured is added; adding from the session start would double count the load and the response wait
            double ResolveTotalPlaySeconds()
            {
                if (!ProgressUtcTime.TryParseIso(header.TotalPlaySecondsCapturedAt, out var capturedAt))
                {
                    AddMissing("totalPlaySeconds", "累計プレイ時間の取得時刻が無いため、今回のプレイ時間を足した概算を記録した");
                    return header.TotalPlaySecondsAtStart + playSeconds;
                }

                var sinceCaptured = (sessionEndUtc - capturedAt).TotalSeconds;
                if (0 <= sinceCaptured) return header.TotalPlaySecondsAtStart + sinceCaptured;

                AddMissing("totalPlaySeconds", $"取得時刻が終了時刻より後のため取得時点の値をそのまま記録した capturedAt:{header.TotalPlaySecondsCapturedAt}");
                return header.TotalPlaySecondsAtStart;
            }

            void AddMissing(string item, string reason)
            {
                Debug.LogWarning($"進行記録の欠損 {item}: {reason}");
                missing.Add(new MissingItem { Item = item, Reason = reason });
            }

            JArray ToJsonArray()
            {
                var array = new JArray();
                foreach (var item in missing) array.Add(new JObject { ["item"] = item.Item, ["reason"] = item.Reason });
                return array;
            }

            #endregion
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
                if (insideBuildMode && entry.Type == ProgressEventType.BlockPlaced)
                    placedInsideBuildMode |= ProgressEvents.TryReadPlacedCount(entry, out var count) && 0 < count;

                if (entry.Type == ProgressEventType.UiStateChanged)
                {
                    var state = ProgressEvents.ReadUiState(entry);
                    var entering = state == PlaceBlockStateName;
                    if (insideBuildMode && !entering && !placedInsideBuildMode) result.Add(ProgressEvents.BuildModeCancelledAt(entry, state));
                    if (!insideBuildMode && entering) placedInsideBuildMode = false;
                    insideBuildMode = entering;
                }

                result.Add(entry);
            }
            return result;
        }
    }
}
