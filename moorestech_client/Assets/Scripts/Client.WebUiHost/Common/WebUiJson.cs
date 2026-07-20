using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Client.WebUiHost.Common
{
    /// <summary>
    /// Web UI 向け JSON シリアライズ設定の一元管理
    /// Centralized JSON serialization settings for the Web UI
    /// </summary>
    public static class WebUiJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, Settings);
        }

        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, Settings);
        }

        // 外部（WS クライアント）由来の JSON をパースする境界。不正入力を bool 化する
        // Boundary for parsing externally-sourced (WS client) JSON; turns malformed input into a bool
        public static bool TryDeserialize<T>(string json, out T value)
        {
            // 不正 JSON・型不一致でのみ throw する JsonException に限定して握り、受信ループを守る
            // Catch only JsonException (malformed JSON / type mismatch) to shield the receive loop
            try
            {
                value = JsonConvert.DeserializeObject<T>(json, Settings);
                return value != null;
            }
            catch (JsonException)
            {
                value = default;
                return false;
            }
        }
    }
}
