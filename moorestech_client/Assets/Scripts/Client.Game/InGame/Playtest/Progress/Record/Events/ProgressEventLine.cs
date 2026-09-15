using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress.Record.Events
{
    // events.jsonl の1行の封筒（t/tick/type/data）と、行を種別ごとのクラスへ戻す唯一の分岐（shared-contracts §3）
    // The envelope of one events.jsonl line (t/tick/type/data) and the only branch turning a line back into its type's class (shared-contracts §3)
    internal static class ProgressEventLine
    {
        public static JObject Envelope(IProgressEvent progressEvent, string type, JObject data)
        {
            return new JObject { ["t"] = progressEvent.T, ["tick"] = progressEvent.Tick, ["type"] = type, ["data"] = data };
        }

        public static string ToJsonLine(IProgressEvent progressEvent)
        {
            return progressEvent.ToJson().ToString(Formatting.None);
        }

        // 壊れた行・未知の種別は理由を出して null を返す。件数は呼び出し側が数えて欠損として記録へ載せる
        // A broken line or an unknown type logs its reason and returns null; the caller counts them into the record as a gap
        public static IProgressEvent FromJsonLine(string line)
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
            var type = json["type"];
            if (json["t"] == null || type == null || type.Type != JTokenType.String || tick == null || tick.Type != JTokenType.Integer || tick.Value<long>() < 0)
            {
                Debug.LogWarning($"進行記録のイベント行の形が違うため飛ばします: {line}");
                return null;
            }

            var t = (string)json["t"];
            var tickValue = (ulong)tick.Value<long>();
            var data = json["data"] as JObject ?? new JObject();
            switch ((string)type)
            {
                case BlockPlacedEvent.TypeName: return BlockPlacedEvent.FromData(t, tickValue, data);
                case UiStateChangedEvent.TypeName: return UiStateChangedEvent.FromData(t, tickValue, data);
                case BuildModeCancelledEvent.TypeName: return BuildModeCancelledEvent.FromData(t, tickValue, data);
                case ChallengeCompletedEvent.TypeName: return ChallengeCompletedEvent.FromData(t, tickValue, data);
                case ResearchCompletedEvent.TypeName: return ResearchCompletedEvent.FromData(t, tickValue, data);
                case CraftCompletedEvent.TypeName: return CraftCompletedEvent.FromData(t, tickValue, data);
                case ReportSentEvent.TypeName: return ReportSentEvent.FromData(t, tickValue, data);
                default:
                    Debug.LogWarning($"進行記録の未知のイベント種別のため飛ばします type:{(string)type}");
                    return null;
            }
        }

        public static bool TryReadString(JObject data, string key, string type, out string value)
        {
            value = null;
            var token = data[key];
            if (token != null && token.Type == JTokenType.String)
            {
                value = (string)token;
                return true;
            }
            Debug.LogWarning($"進行記録のイベント行に文字列の {key} が無いため飛ばします type:{type}");
            return false;
        }

        public static bool TryReadInt(JObject data, string key, string type, out int value)
        {
            value = 0;
            var token = data[key];
            if (token != null && token.Type == JTokenType.Integer)
            {
                value = (int)token;
                return true;
            }
            Debug.LogWarning($"進行記録のイベント行に整数の {key} が無いため飛ばします type:{type}");
            return false;
        }
    }
}
