using System;
using System.Collections.Generic;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.Playtest.WebUi
{
    public class DomQueryResult
    {
        public bool Found;
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public float DevicePixelRatio;
        public bool HitTestPassed;
    }

    /// <summary>
    ///     Web UIへDOM矩形を問い合わせ、CEF座標をUnity座標へ橋渡しする
    ///     Queries DOM rectangles from the Web UI and bridges CEF coordinates to Unity
    /// </summary>
    public static class PlaytestDomQuery
    {
        private const string QueryTopic = "playtest.dom_query";
        private static readonly Dictionary<string, DomQueryResult> Pending = new();
        private static readonly DomQueryResultActionHandler Handler = new();
        private static WebSocketHub _registeredHub;

        public static void RegisterHandler()
        {
            var hub = global::Client.WebUiHost.Boot.WebUiHost.Hub;

            // 各実行の保留結果を消し、同一Hubへの二重登録警告を避ける
            // Clear per-run pending results and avoid duplicate registration warnings on the same hub
            ResetPending();
            if (hub == null || ReferenceEquals(hub, _registeredHub)) return;
            hub.RegisterAction(Handler);
            _registeredHub = hub;
        }

        public static void ResetPending()
        {
            Pending.Clear();
        }

        public static async UniTask<DomQueryResult> Query(string testid, float timeoutSeconds)
        {
            var hub = global::Client.WebUiHost.Boot.WebUiHost.Hub;
            if (hub == null) return new DomQueryResult();

            // requestIdを先に保留登録してからevent配信し、即時応答も取りこぼさない
            // Mark the request pending before publishing so even an immediate response is retained
            var requestId = Guid.NewGuid().ToString();
            Pending[requestId] = null;
            hub.Publish(QueryTopic, WebUiJson.Serialize(new DomQueryRequest
            {
                RequestId = requestId,
                Testid = testid,
            }));

            // 応答をフレーム単位で待ち、タイムアウトは未発見結果として呼び出し側へ返す
            // Poll for the response per frame and return not-found on timeout for caller-side retries
            var startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt <= timeoutSeconds)
            {
                if (Pending.TryGetValue(requestId, out var result) && result != null)
                {
                    Pending.Remove(requestId);
                    return result;
                }
                await UniTask.Yield();
            }

            Pending.Remove(requestId);
            return new DomQueryResult();
        }

        public static bool TryGetScreenCenter(DomQueryResult result, out Vector2 screenPosition)
        {
            screenPosition = default;
            if (result == null || !IsValidResult(result) || !result.Found || !result.HitTestPassed) return false;

            // CSS pxの矩形中心をdevicePixelRatioでCEFブラウザpxへ換算する
            // Convert the CSS-pixel rectangle center to CEF browser pixels with devicePixelRatio
            var browserPosition = new Vector2(
                (result.X + result.Width * 0.5f) * result.DevicePixelRatio,
                (result.Y + result.Height * 0.5f) * result.DevicePixelRatio);
            if (!IsFinite(browserPosition.x) || !IsFinite(browserPosition.y)) return false;
            return CefScreenMapper.TryBrowserToScreen(browserPosition, out screenPosition);
        }

        private static bool IsValidResult(DomQueryResult result)
        {
            if (!IsFinite(result.X) || !IsFinite(result.Y) ||
                !IsFinite(result.Width) || !IsFinite(result.Height) ||
                !IsFinite(result.DevicePixelRatio)) return false;
            if (result.Width < 0f || result.Height < 0f || result.DevicePixelRatio <= 0f) return false;
            return !result.HitTestPassed || (0f < result.Width && 0f < result.Height);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private class DomQueryRequest
        {
            public string RequestId;
            public string Testid;
        }

        private class DomQueryResultActionHandler : IActionHandler
        {
            public string ActionType => "playtest.dom_query_result";

            public UniTask<ActionResult> ExecuteAsync(JObject payload)
            {
                // WS外部入力を型ごとに検証し、不正な応答を保留辞書へ混入させない
                // Validate each externally sourced WS value before admitting it to the pending dictionary
                if (!TryReadString("requestId", out var requestId) ||
                    !TryReadBool("found", out var found) ||
                    !TryReadFloat("x", out var x) ||
                    !TryReadFloat("y", out var y) ||
                    !TryReadFloat("width", out var width) ||
                    !TryReadFloat("height", out var height) ||
                    !TryReadFloat("devicePixelRatio", out var devicePixelRatio) ||
                    !TryReadBool("hitTestPassed", out var hitTestPassed))
                {
                    return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
                }

                // 検証対象を組み立て、数値の相関制約を保留辞書への格納前に評価する
                // Build the candidate and evaluate cross-field numeric constraints before pending storage
                var result = new DomQueryResult
                {
                    Found = found,
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height,
                    DevicePixelRatio = devicePixelRatio,
                    HitTestPassed = hitTestPassed,
                };
                if (!IsValidResult(result)) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));

                // タイムアウト済みの遅延応答は捨て、現在待機中のrequestIdだけを解決する
                // Drop late responses after timeout and resolve only requestIds that are still pending
                if (Pending.ContainsKey(requestId))
                {
                    Pending[requestId] = result;
                }
                return UniTask.FromResult(ActionResult.Success());

                #region Internal

                bool TryReadString(string name, out string value)
                {
                    value = null;
                    if (payload?[name] is not JValue { Type: JTokenType.String } token) return false;
                    value = (string)token;
                    return !string.IsNullOrEmpty(value);
                }

                bool TryReadBool(string name, out bool value)
                {
                    value = false;
                    if (payload?[name] is not JValue { Type: JTokenType.Boolean } token) return false;
                    value = (bool)token;
                    return true;
                }

                bool TryReadFloat(string name, out float value)
                {
                    value = 0f;
                    var token = payload?[name];
                    if (token?.Type != JTokenType.Float && token?.Type != JTokenType.Integer) return false;
                    value = (float)token;
                    return IsFinite(value);
                }

                #endregion
            }
        }
    }
}
