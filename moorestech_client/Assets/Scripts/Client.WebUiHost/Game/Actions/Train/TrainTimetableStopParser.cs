using Game.Train.RailGraph;
using Game.Train.Unit;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions
{
    // 停車駅は整数座標（Int32範囲）と端 "front"/"back" が揃ったものだけ受け取る
    // Accept a stop only with integer coordinates within Int32 and a side of "front" or "back"
    internal static class TrainTimetableStopParser
    {
        public static bool TryParse(JToken token, out TrainTimetableStop stop)
        {
            stop = default;
            if (token is not JObject station ||
                !TryReadCoordinate(station["x"], out var x) ||
                !TryReadCoordinate(station["y"], out var y) ||
                !TryReadCoordinate(station["z"], out var z) ||
                !TrainTimetableStopSideWire.TryParse(station["side"], out var side)) return false;
            stop = new TrainTimetableStop(new Vector3Int(x, y, z), side);
            return true;

            #region Internal

            bool TryReadCoordinate(JToken coordinateToken, out int coordinate)
            {
                coordinate = 0;
                return coordinateToken is JValue { Type: JTokenType.Integer } && int.TryParse(coordinateToken.ToString(), out coordinate);
            }

            #endregion
        }
    }
}
