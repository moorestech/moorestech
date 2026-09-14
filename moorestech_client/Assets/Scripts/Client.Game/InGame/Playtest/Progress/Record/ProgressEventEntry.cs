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

        // 追記中に落ちた行は壊れうる。読み側の境界なのでここだけcatchし、壊れた行は捨てて件数を呼び出し側へ返す
        // A line can be torn by a crash mid-append; this read boundary catches, drops the bad line and lets the caller count it
        public static ProgressEventEntry FromJsonLine(string line)
        {
            // JObject.Parseは外部入力JSONのパース境界。追記中断で壊れた行が来てもここだけで吸収する
            // JObject.Parse is the boundary for parsing externally-sourced JSON; a line torn by a mid-append crash is absorbed right here
            try
            {
                var json = JObject.Parse(line);
                return new ProgressEventEntry
                {
                    T = (string)json["t"],
                    Tick = (ulong)json["tick"],
                    Type = (string)json["type"],
                    Data = json["data"] as JObject ?? new JObject(),
                };
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"進行記録のイベント行を読めないため飛ばします: {exception.GetBaseException().Message}");
                return null;
            }
        }
    }
}
