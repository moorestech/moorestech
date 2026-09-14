using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress
{
    // 進行記録のイベント種別。文字列は受け口・集計側と共有する契約値（shared-contracts §3）
    // Progress event types; these strings are the contract shared with the receiver and the digest (shared-contracts §3)
    internal static class ProgressEventType
    {
        public const string ResearchCompleted = "researchCompleted";
        public const string ChallengeCompleted = "challengeCompleted";
        public const string UiStateChanged = "uiStateChanged";
        public const string BuildModeCancelled = "buildModeCancelled";
        public const string BlockPlaced = "blockPlaced";
        public const string ReportSent = "reportSent";

        // 送信しただけで結果は見ていない。素材不足でサーバーに拒否された要求もこれに載る
        // Only the request was sent and its outcome is unseen; a request the server rejected for missing materials still lands here
        public const string CraftRequested = "craftRequested";
    }

    internal sealed class ProgressEventEntry
    {
        public string T;
        public ulong Tick;
        public string Type;
        public JObject Data;

        public static ProgressEventEntry Create(DateTime utc, ulong tick, string type, JObject data)
        {
            return new ProgressEventEntry { T = ProgressUtcTime.ToIso(utc), Tick = tick, Type = type, Data = data ?? new JObject() };
        }

        public string ToJsonLine()
        {
            return ToJObject().ToString(Newtonsoft.Json.Formatting.None);
        }

        public JObject ToJObject()
        {
            return new JObject { ["t"] = T, ["tick"] = Tick, ["type"] = Type, ["data"] = Data };
        }

        // 壊れた行は捨てて null を返す。件数は呼び出し側が数えて欠損として記録へ載せる
        // A broken line is dropped as null; the caller counts them and puts the gap into the record
        public static ProgressEventEntry FromJsonLine(string line)
        {
            // catchするのは JObject.Parse だけ。追記中断で千切れた行はここでしか判別できない外部入力のパース境界
            // Only JObject.Parse is caught: a line torn by a mid-append crash is externally-sourced input whose damage shows up nowhere else
            JObject json;
            try
            {
                json = JObject.Parse(line);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"進行記録のイベント行を読めないため飛ばします: {exception.GetBaseException().Message}");
                return null;
            }

            // 形の検証は自分が書いた行の点検なので例外では吸わない。欠けたキー・負のtickは理由を出して1行だけ捨てる
            // Validating the shape inspects lines we wrote ourselves, so no exception absorbs it; a missing key or a negative tick drops just that line with a reason
            var tick = json["tick"];
            if (json["t"] == null || json["type"] == null || tick == null || tick.Type != JTokenType.Integer || tick.Value<long>() < 0)
            {
                Debug.LogWarning($"進行記録のイベント行の形が違うため飛ばします: {line}");
                return null;
            }

            return new ProgressEventEntry
            {
                T = (string)json["t"],
                Tick = (ulong)tick.Value<long>(),
                Type = (string)json["type"],
                Data = json["data"] as JObject ?? new JObject(),
            };
        }
    }
}
