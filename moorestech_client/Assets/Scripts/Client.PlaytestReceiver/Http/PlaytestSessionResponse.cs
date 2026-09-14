using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.PlaytestReceiver.Http
{
    // POST /v1/session の200応答。生成はParse経由だけに閉じ、steamIdとtokenが揃った応答しか作れない
    // The 200 body of POST /v1/session; Parse is the only constructor, so an instance always carries steamId and token
    public sealed class PlaytestSessionResponse
    {
        private PlaytestSessionResponse(string steamId, string token, bool allowed)
        {
            SteamId = steamId;
            Token = token;
            Allowed = allowed;
        }

        public string SteamId { get; }
        public string Token { get; }
        public bool Allowed { get; }

        public static PlaytestSessionResponse Parse(string body)
        {
            string steamId;
            string token;
            bool allowed;

            // 受け口の本文は外部入力のJSON。キャプティブポータルは200でHTMLを返すため、この境界で畳んでnullにする
            // The receiver's body is external-input JSON; captive portals answer 200 with HTML, so this boundary folds it to null
            try
            {
                var parsed = JObject.Parse(body);
                steamId = (string)parsed["steamId"];
                token = (string)parsed["token"];
                allowed = (bool?)parsed["allowed"] ?? false;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlaytestReceiver] session response was not the expected JSON: {exception.GetBaseException().Message}");
                return null;
            }

            // トークンの無い200で通すと、照合だけ通ってアップロードが全滅する。欠落は許可しない
            // A 200 without a token would pass the gate and then fail every upload, so a missing field is refused
            if (string.IsNullOrEmpty(steamId) || string.IsNullOrEmpty(token))
            {
                Debug.LogWarning("[PlaytestReceiver] session response lacked steamId or token");
                return null;
            }

            return new PlaytestSessionResponse(steamId, token, allowed);
        }
    }
}
