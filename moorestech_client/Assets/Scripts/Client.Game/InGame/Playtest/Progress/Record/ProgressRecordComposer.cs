using System;
using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.Playtest.Progress.Record.Events;
using Client.Game.InGame.UI.UIState;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress.Record
{
    // ヘッダとイベント列から record.json を組む純関数。集計もキャンセル合成もここだけで行う（shared-contracts §3）
    // A pure function composing record.json from the header and events; all aggregation and cancel synthesis live here (shared-contracts §3)
    internal static class ProgressRecordComposer
    {
        // 建築モードの判定はUI状態のSSOTから引く。文字列を持つと状態名の改名で合成が無音で止まる
        // The build-mode check comes from the UI state SSOT; a literal would silently stop the synthesis when the state is renamed
        private const string PlaceBlockStateName = nameof(UIStateEnum.PlaceBlock);

        public static string Compose(ProgressRecordHeader header, IReadOnlyList<IProgressEvent> events, ProgressEndReason endReason, DateTime sessionEndUtc)
        {
            var enriched = WithSynthesizedBuildModeCancel(events);
            var missing = new List<MissingItem>(header.Missing);
            var aggregate = ProgressRecordAggregate.From(header, enriched);
            missing.AddRange(aggregate.Missing);

            var playSeconds = ResolvePlaySeconds();
            var totalPlaySeconds = ResolveTotalPlaySeconds();

            // 設置数は区間ごとの集計で、終了flushを通らないcrash-recoveredでは最後のUI遷移以降のぶんが記録に残らない（ADR 0060 裁定9の代償）
            // Placements are aggregated per interval, and crash-recovered never passes the shutdown flush, so whatever followed the last UI transition is gone (the cost of ADR 0060 adjudication 9)
            if (endReason == ProgressEndReason.CrashRecovered) AddMissing("placedBlockCount", "最後のUI状態遷移以降に置かれたブロック数は記録されていない（集計の書き出し前に異常終了した）");

            var eventArray = new JArray();
            foreach (var progressEvent in enriched) eventArray.Add(progressEvent.ToJson());

            // 取れなかった値は null で出す。空文字や0は実値と同じ形になり、欠損列と食い違って読める（F02）
            // Unavailable values go out as null; an empty string or 0 would share the shape of real data and contradict the missing column (F02)
            var record = new JObject
            {
                ["schemaVersion"] = header.SchemaVersion,
                ["steamId"] = NullableText(header.SteamId),
                ["buildInfo"] = header.BuildInfo == null ? JValue.CreateNull() : JObject.Parse(JsonConvert.SerializeObject(header.BuildInfo, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() })),
                ["sessionStart"] = NullableText(header.SessionStart),
                ["sessionEnd"] = ProgressUtcTime.ToIso(sessionEndUtc),
                ["endReason"] = ProgressEndReasonJson.ToContractText(endReason),
                ["playSeconds"] = NullableNumber(playSeconds),
                ["worldCreatedAt"] = NullableText(header.WorldCreatedAt),
                ["totalPlaySeconds"] = NullableNumber(totalPlaySeconds),
                ["reachedChallenges"] = new JArray(aggregate.ReachedChallenges),
                ["completedResearch"] = new JArray(aggregate.CompletedResearch),
                ["placedBlockCount"] = aggregate.PlacedBlockCount,
                ["craftCount"] = aggregate.CraftCount,
                ["lastUiState"] = NullableText(aggregate.LastUiState),
                ["missing"] = ToJsonArray(),
                ["events"] = eventArray,
            };
            return record.ToString(Formatting.Indented);

            #region Internal

            // 開始時刻が読めない・終了より後ろ、のどちらも実データと同じ0秒へ潰さず null と理由を残す
            // Neither an unreadable start nor one later than the end collapses into a plain 0 seconds; null and the reason are kept
            double? ResolvePlaySeconds()
            {
                if (!ProgressUtcTime.TryParseIso(header.SessionStart, out var sessionStart))
                {
                    AddMissing("playSeconds", $"セッション開始時刻を読めない value:{header.SessionStart}");
                    return null;
                }

                var seconds = (sessionEndUtc - sessionStart).TotalSeconds;
                if (0 <= seconds) return seconds;

                AddMissing("playSeconds", $"終了時刻が開始時刻より前 start:{header.SessionStart} end:{ProgressUtcTime.ToIso(sessionEndUtc)}");
                return null;
            }

            // 累計は「取得した瞬間からの経過」だけを足す。セッション開始起点で足すとロードと応答待ちのぶんが二重に乗る
            // Only the span since the value was captured is added; adding from the session start would double count the load and the response wait
            double? ResolveTotalPlaySeconds()
            {
                if (header.TotalPlaySecondsAtStart == null)
                {
                    // 取得できなかった理由は書き手が既に積んでいる。ヘッダを失った残骸だけは理由が無いのでここで名乗る
                    // The writer already declared why it was unavailable; only a leftover that lost its header has no reason yet, so it is declared here
                    if (!HasMissing("totalPlaySeconds")) AddMissing("totalPlaySeconds", "累計プレイ時間の取得値がヘッダに無い");
                    return null;
                }

                if (!ProgressUtcTime.TryParseIso(header.TotalPlaySecondsCapturedAt, out var capturedAt))
                {
                    AddMissing("totalPlaySeconds", $"累計プレイ時間の取得時刻を読めない value:{header.TotalPlaySecondsCapturedAt}");
                    return null;
                }

                var sinceCaptured = (sessionEndUtc - capturedAt).TotalSeconds;
                if (0 <= sinceCaptured) return header.TotalPlaySecondsAtStart + sinceCaptured;

                AddMissing("totalPlaySeconds", $"取得時刻が終了時刻より後のため取得時点の値をそのまま記録した capturedAt:{header.TotalPlaySecondsCapturedAt}");
                return header.TotalPlaySecondsAtStart;
            }

            bool HasMissing(string item)
            {
                foreach (var existing in missing)
                    if (existing.Item == item) return true;
                return false;
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

        // 「PlaceBlock に入って、本人が1件も設置しないまま抜けた」滞在だけを離脱として合成する
        // Synthesizes a cancel only for a stay that entered PlaceBlock and left without this player placing anything
        public static List<IProgressEvent> WithSynthesizedBuildModeCancel(IReadOnlyList<IProgressEvent> events)
        {
            var result = new List<IProgressEvent>(events.Count + 4);
            var insideBuildMode = false;
            var placedInsideBuildMode = false;

            foreach (var progressEvent in events)
            {
                if (insideBuildMode && progressEvent is BlockPlacedEvent placed) placedInsideBuildMode |= 0 < placed.Count;

                if (progressEvent is UiStateChangedEvent transition)
                {
                    var entering = transition.State == PlaceBlockStateName;
                    if (insideBuildMode && !entering && !placedInsideBuildMode) result.Add(BuildModeCancelledEvent.CausedBy(transition));
                    if (!insideBuildMode && entering) placedInsideBuildMode = false;
                    insideBuildMode = entering;
                }

                result.Add(progressEvent);
            }
            return result;
        }

        private static JToken NullableText(string value)
        {
            return value == null ? JValue.CreateNull() : new JValue(value);
        }

        private static JToken NullableNumber(double? value)
        {
            return value.HasValue ? new JValue(value.Value) : JValue.CreateNull();
        }
    }
}
