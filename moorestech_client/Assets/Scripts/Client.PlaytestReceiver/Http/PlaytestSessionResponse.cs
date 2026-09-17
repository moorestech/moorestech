using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.PlaytestReceiver.Http
{
    // POST /v1/session の200応答。生成はParse経由だけに閉じ、tokenと期限が揃った応答しか作れない
    // The 200 body of POST /v1/session; Parse is the only constructor, so an instance always carries a token and its expiry
    internal sealed class PlaytestSessionResponse
    {
        private PlaytestSessionResponse(string token, bool allowed, DateTime expiresAtUtc)
        {
            Token = token;
            Allowed = allowed;
            ExpiresAtUtc = expiresAtUtc;
        }

        public string Token { get; }
        public bool Allowed { get; }
        public DateTime ExpiresAtUtc { get; }

        public static PlaytestSessionResponse Parse(string body)
        {
            string token;
            bool allowed;
            string expiresAtText;

            // 受け口の本文は外部入力のJSON。キャプティブポータルは200でHTMLを返すため、この境界で畳んでnullにする
            // The receiver's body is external-input JSON; captive portals answer 200 with HTML, so this boundary folds it to null
            try
            {
                var parsed = JsonConvert.DeserializeObject<JObject>(body, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
                token = (string)parsed["token"];
                allowed = (bool?)parsed["allowed"] ?? false;
                expiresAtText = (string)parsed["expiresAt"];
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlaytestReceiver] session response was not the expected JSON: {exception.GetBaseException().Message}");
                return null;
            }

            // トークンの無い200で通すと、照合だけ通ってアップロードが全滅する。欠落は許可しない
            // A 200 without a token would pass the gate and then fail every upload, so a missing field is refused
            if (string.IsNullOrEmpty(token))
            {
                Debug.LogWarning("[PlaytestReceiver] session response lacked token");
                return null;
            }

            if (!DateTimeOffset.TryParse(expiresAtText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expiresAt))
            {
                Debug.LogWarning($"[PlaytestReceiver] session response had no readable expiresAt: '{expiresAtText}'");
                return null;
            }

            return new PlaytestSessionResponse(token, allowed, expiresAt.UtcDateTime);
        }
    }
}
