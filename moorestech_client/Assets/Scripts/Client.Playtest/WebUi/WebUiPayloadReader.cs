using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Client.Playtest.WebUi
{
    /// <summary>
    ///     Web UIから届くaction payload（外部入力）を型ごとに読む
    ///     Reads action payloads arriving from the Web UI (external input) field by field
    /// </summary>
    internal static class WebUiPayloadReader
    {
        public static bool TryReadNonEmptyString(JObject payload, string name, out string value)
        {
            return TryReadString(payload, name, out value) && value.Length != 0;
        }

        public static bool TryReadString(JObject payload, string name, out string value)
        {
            value = null;
            if (payload?[name] is not JValue { Type: JTokenType.String } token) return false;
            value = (string)token;
            return true;
        }

        public static bool TryReadBool(JObject payload, string name, out bool value)
        {
            value = false;
            if (payload?[name] is not JValue { Type: JTokenType.Boolean } token) return false;
            value = (bool)token;
            return true;
        }

        public static bool TryReadFloat(JObject payload, string name, out float value)
        {
            value = 0f;
            var token = payload?[name];
            if (token?.Type != JTokenType.Float && token?.Type != JTokenType.Integer) return false;
            // JSON表現は現在のカルチャに左右されないので、数値parseにも同じカルチャを使う
            // JSON formatting is culture-independent, so parse numbers with the matching culture
            return float.TryParse(token.ToString(Newtonsoft.Json.Formatting.None), NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                   !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static bool TryReadNonNegativeInt(JObject payload, string name, out int value)
        {
            value = 0;
            if (payload?[name] is not JValue { Type: JTokenType.Integer } token) return false;
            // 外部JSONの整数はint範囲を超え得るため、変換例外を起こさず拒否する
            // External JSON integers can exceed int range, so reject them without a conversion exception
            if (!long.TryParse(token.ToString(Newtonsoft.Json.Formatting.None), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return false;
            if (parsed < 0 || int.MaxValue < parsed) return false;
            value = (int)parsed;
            return true;
        }

        public static bool TryReadStringArray(JObject payload, string name, out string[] values)
        {
            values = null;
            if (payload?[name] is not JArray array) return false;

            // 要素が1つでも文字列でなければ配列ごと不正とする
            // Reject the whole array when any element is not a string
            var list = new List<string>(array.Count);
            foreach (var item in array)
            {
                if (item is not JValue { Type: JTokenType.String } stringToken) return false;
                list.Add((string)stringToken);
            }
            values = list.ToArray();
            return true;
        }
    }
}
