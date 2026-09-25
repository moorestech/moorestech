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
        private PlaytestSessionResponse(string steamId, string token, DateTime expiresAtUtc)
        {
            SteamId = steamId;
            Token = token;
            ExpiresAtUtc = expiresAtUtc;
        }

        // 受け口がSteam Web APIで検証したSteamID。送信先のアカウントを示す（ADR 0070）
        // The SteamID the receiver verified through the Steam Web API; it identifies the receiving account (ADR 0070)
        public string SteamId { get; }
        public string Token { get; }
        public DateTime ExpiresAtUtc { get; }

        public static PlaytestSessionResponse Parse(string body)
        {
            string steamId;
            string token;
            string expiresAtText;

            // 受け口の本文は外部入力のJSON。キャプティブポータルは200でHTMLを返すため、この境界で畳んでnullにする
            // The receiver's body is external-input JSON; captive portals answer 200 with HTML, so this boundary folds it to null
            try
            {
                var parsed = JsonConvert.DeserializeObject<JObject>(body, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
                steamId = (string)parsed["steamId"];
                token = (string)parsed["token"];
                expiresAtText = (string)parsed["expiresAt"];
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlaytestReceiver] session response was not the expected JSON: {exception.GetBaseException().Message}");
                return null;
            }

            // 送信先を特定できない応答を受け付けない。SteamIDの欠落も空文字も契約違反とする
            // Reject responses without an identified receiving account; a missing or empty SteamID breaks the contract
            if (string.IsNullOrWhiteSpace(steamId))
            {
                Debug.LogWarning("[PlaytestReceiver] session response lacked steamId");
                return null;
            }

            // トークンの無い200ではアップロードできない。欠落・空白は許可しない
            // A 200 without a token cannot upload, so a missing or blank field is refused
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

            return new PlaytestSessionResponse(steamId, token, expiresAt.UtcDateTime);
        }
    }
}
