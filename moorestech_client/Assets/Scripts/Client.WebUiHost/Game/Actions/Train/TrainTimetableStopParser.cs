using Core.Update;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using Game.Train.Unit;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions
{
    // 停車駅は整数座標(Int32)と端"front"/"back"が揃う時だけ通す
    // Accept a stop only with integer coordinates within Int32 and a side of "front" or "back"
    internal static class TrainTimetableStopParser
    {
        // UIは出発条件を持たないので1秒待機を固定で積む（条件選択は後続issue）
        // The UI has no departure condition yet, so a fixed one-second wait is used (selection comes later)
        private const TrainDiagram.DepartureConditionType UiFixedDepartureCondition = TrainDiagram.DepartureConditionType.WaitForTicks;

        public static bool TryParse(JToken token, out TrainTimetableStop stop)
        {
            stop = default;
            if (token is not JObject station ||
                !TryReadCoordinate(station["x"], out var x) ||
                !TryReadCoordinate(station["y"], out var y) ||
                !TryReadCoordinate(station["z"], out var z) ||
                !TrainTimetableStopSideWire.TryParse(station["side"], out var side)) return false;
            stop = new TrainTimetableStop(new Vector3Int(x, y, z), side, UiFixedDepartureCondition, GameUpdater.TicksPerSecond);
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
