using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Client.PlaytestReceiver.Http.Responses
{
    // 受け口の応答本文から判別に要る値だけを読む。外部入力のJSONなので形が違えば null / false に畳む
    // Reads only the values needed to tell receiver answers apart; the body is external JSON, so a wrong shape folds to null / false
    public static class PlaytestReceiverResponseBody
    {
        // 失敗応答の reason（{"reason":"..."}）。読めなければ null
        // The reason of a failure answer ({"reason":"..."}); null when unreadable
        public static string ReadReason(string body)
        {
            var root = TryParseObject(body);
            return root?["reason"] is JValue { Type: JTokenType.String } reason ? (string)reason : null;
        }

        // complete の成功応答は {"ready":true} だけ。それ以外の2xxは契約違反として扱わせる
        // complete's only success body is {"ready":true}; any other 2xx is left for the caller to treat as a contract breach
        public static bool IsReady(string body)
        {
            var root = TryParseObject(body);
            return root?["ready"] is JValue { Type: JTokenType.Boolean } ready && (bool)ready;
        }

        // 外部入力JSONのパース境界。HTMLや空本文は例外になるので、ここで null に畳む
        // The parse boundary for external JSON; HTML or an empty body throws, so it folds to null here
        private static JObject TryParseObject(string body)
        {
            try
            {
                return JToken.Parse(body) as JObject;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
