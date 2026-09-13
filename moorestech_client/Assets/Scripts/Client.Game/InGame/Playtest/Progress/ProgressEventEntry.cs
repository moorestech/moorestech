using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress
{
    // 進行記録のイベント種別。文字列は受け口・集計側と共有する契約値（shared-contracts §3）
    // Progress event types; these strings are the contract shared with the receiver and the digest (shared-contracts §3)
    public static class ProgressEventType
    {
        public const string ResearchCompleted = "researchCompleted";
        public const string ChallengeCompleted = "challengeCompleted";
        public const string UiStateChanged = "uiStateChanged";
        public const string BuildModeCancelled = "buildModeCancelled";
        public const string BlockPlaced = "blockPlaced";
        public const string ReportSent = "reportSent";
        public const string CraftExecuted = "craftExecuted";
    }

    public sealed class ProgressEventEntry
    {
        public string T;
        public ulong Tick;
        public string Type;
        public JObject Data;

        public static ProgressEventEntry Create(DateTime utc, ulong tick, string type, JObject data)
        {
            return new ProgressEventEntry { T = utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"), Tick = tick, Type = type, Data = data ?? new JObject() };
        }

        public string ToJsonLine()
        {
            return new JObject { ["t"] = T, ["tick"] = Tick, ["type"] = Type, ["data"] = Data }.ToString(Newtonsoft.Json.Formatting.None);
        }

        public JObject ToJObject()
        {
            return new JObject { ["t"] = T, ["tick"] = Tick, ["type"] = Type, ["data"] = Data };
        }

        // 追記中に落ちた行は壊れうる。読み側の境界なのでここだけcatchし、壊れた行は捨てて続行する
        // A line can be torn by a crash mid-append; this read boundary catches, drops the bad line and continues
        public static ProgressEventEntry FromJsonLine(string line)
        {
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
