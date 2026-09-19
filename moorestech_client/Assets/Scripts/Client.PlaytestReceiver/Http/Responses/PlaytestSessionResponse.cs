using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.PlaytestReceiver.Http.Responses
{
    // POST /v1/session の200応答。生成はParse経由だけに閉じ、steamId・token・期限が揃った応答しか作れない
    // The 200 body of POST /v1/session; Parse is the only constructor, so an instance always carries a steamId, a token and its expiry
    internal sealed class PlaytestSessionResponse
    {
        private PlaytestSessionResponse(string steamId, string token, bool allowed, DateTime expiresAtUtc)
        {
            SteamId = steamId;
            Token = token;
            Allowed = allowed;
            ExpiresAtUtc = expiresAtUtc;
        }

        // 受け口がSteam Web APIで検証したSteamID。報告・進行記録・異常終了箱の識別になる（ADR 0065）
        // The SteamID the receiver verified through the Steam Web API; it becomes the identity of reports, progress records and crash boxes (ADR 0065)
        public string SteamId { get; }
        public string Token { get; }
        public bool Allowed { get; }
        public DateTime ExpiresAtUtc { get; }

        public static PlaytestSessionResponse Parse(string body)
        {
            string steamId;
            string token;
            bool allowed;
            string expiresAtText;

            // 受け口の本文は外部入力のJSON。キャプティブポータルは200でHTMLを返すため、この境界で畳んでnullにする
            // The receiver's body is external-input JSON; captive portals answer 200 with HTML, so this boundary folds it to null
            try
            {
                var parsed = JsonConvert.DeserializeObject<JObject>(body, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
                steamId = (string)parsed["steamId"];
                token = (string)parsed["token"];
                allowed = (bool?)parsed["allowed"] ?? false;
                expiresAtText = (string)parsed["expiresAt"];
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlaytestReceiver] session response was not the expected JSON: {exception.GetBaseException().Message}");
                return null;
            }

            // 誰の記録かを載せられない200で通すと、識別が空のまま報告と進行記録が走る。欠落も空文字も許可しない（ADR 0065）
            // A 200 that cannot name the tester would run reports and progress records with no identity; neither a missing nor an empty value is accepted (ADR 0065)
            if (string.IsNullOrWhiteSpace(steamId))
            {
                Debug.LogWarning("[PlaytestReceiver] session response lacked steamId");
                return null;
            }

            // トークンの無い200で通すと、照合だけ通ってアップロードが全滅する。欠落・空白は許可しない
            // A 200 without a token would pass the gate and then fail every upload, so a missing or blank field is refused
            if (string.IsNullOrWhiteSpace(token))
            {
                Debug.LogWarning("[PlaytestReceiver] session response lacked token");
                return null;
            }

            if (!DateTimeOffset.TryParse(expiresAtText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expiresAt))
            {
                Debug.LogWarning($"[PlaytestReceiver] session response had no readable expiresAt: '{expiresAtText}'");
                return null;
            }

            return new PlaytestSessionResponse(steamId, token, allowed, expiresAt.UtcDateTime);
        }
    }
}
