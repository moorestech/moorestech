using Game.Train.RailGraph;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions
{
    // StationNodeSideとwire文字列"front"/"back"の対応表
    // Centralizes the mapping between StationNodeSide and the wire strings "front"/"back"
    internal static class TrainTimetableStopSideWire
    {
        // 未知値はnullでfail-closedに倒す（無言で"back"を出さない）
        // Unknown values return null so the caller fails closed instead of silently emitting "back"
        public static string ToWire(StationNodeSide side)
        {
            switch (side)
            {
                case StationNodeSide.Front:
                    return "front";
                case StationNodeSide.Back:
                    return "back";
                default:
                    Debug.LogError($"[TrainTimetableStopSideWire] unknown StationNodeSide: {side}");
                    return null;
            }
        }

        public static bool TryParse(JToken token, out StationNodeSide side)
        {
            side = default;
            if (token is not JValue { Type: JTokenType.String }) return false;
            switch ((string)token)
            {
                case "front":
                    side = StationNodeSide.Front;
                    return true;
                case "back":
                    side = StationNodeSide.Back;
                    return true;
                default:
                    return false;
            }
        }
    }
}
