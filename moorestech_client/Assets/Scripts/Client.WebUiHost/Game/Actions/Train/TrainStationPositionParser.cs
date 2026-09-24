using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions
{
    // JSON座標は整数型とInt32範囲を満たすものだけ受け取る
    // Accept JSON coordinates only when they are integers within the Int32 range
    internal static class TrainStationPositionParser
    {
        public static bool TryParse(JToken token, out Vector3Int position)
        {
            position = default;
            if (token is not JObject station ||
                !TryReadCoordinate(station["x"], out var x) ||
                !TryReadCoordinate(station["y"], out var y) ||
                !TryReadCoordinate(station["z"], out var z)) return false;
            position = new Vector3Int(x, y, z);
            return true;
        }

        private static bool TryReadCoordinate(JToken token, out int coordinate)
        {
            coordinate = 0;
            return token is JValue { Type: JTokenType.Integer } && int.TryParse(token.ToString(), out coordinate);
        }
    }
}
