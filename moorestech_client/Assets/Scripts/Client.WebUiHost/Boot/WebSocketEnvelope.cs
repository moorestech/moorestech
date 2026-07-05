using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// WS で返す JSON 封筒（event/snapshot/result）を組み立てる純関数群
    /// Pure builders for the WS JSON envelopes (event/snapshot/result)
    /// </summary>
    internal static class WebSocketEnvelope
    {
        // action 応答の result 封筒を作る
        // Build the result envelope for an action response
        public static string BuildResult(string requestId, bool ok, string error)
        {
            var env = new JObject
            {
                ["op"] = "result",
                ["requestId"] = requestId,
                ["ok"] = ok,
            };
            if (error != null) env["error"] = error;
            return env.ToString(Formatting.None);
        }

        // event/snapshot 封筒を作る。data はパース済み JSON をそのまま埋め込む
        // Build the event/snapshot envelope; data embeds the pre-parsed JSON as-is
        public static string BuildEnvelope(string op, string topic, string dataJson)
        {
            // 日付風文字列の暗黙 DateTime 変換を無効化し、データを素通しする
            // Disable implicit DateTime parsing so date-like strings pass through untouched
            var reader = new JsonTextReader(new StringReader(dataJson)) { DateParseHandling = DateParseHandling.None };
            var data = JToken.ReadFrom(reader);
            var env = new JObject
            {
                ["op"] = op,
                ["topic"] = topic,
                ["data"] = data,
            };
            return env.ToString(Formatting.None);
        }
    }
}
