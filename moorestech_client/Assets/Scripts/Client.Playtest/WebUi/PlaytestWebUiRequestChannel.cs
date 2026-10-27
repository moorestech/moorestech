using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Cef;
using Client.WebUiHost.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.Playtest.WebUi
{
    internal enum WebUiResponseState { Pending, Responded, Rejected, TimedOut, Stopped }

    // 要求の終端状態と拒否理由を保持する
    // Preserve terminal request state and its rejection reason
    internal readonly struct WebUiChannelResponse<TResult> where TResult : class
    {
        public readonly WebUiResponseState State;
        public readonly TResult Result;
        public readonly string Reason;
        public bool HasResponse => State == WebUiResponseState.Responded;

        public WebUiChannelResponse(WebUiResponseState state, TResult result, string reason)
        {
            State = state;
            Result = result;
            Reason = reason;
        }
    }

    internal class PlaytestWebUiRequestChannel<TResult> where TResult : class
    {
        private readonly Dictionary<string, PendingRequest> _pending = new();

        // 登録済みCEF接続へ要求を送り、終端状態まで待つ。本文には要求IDを足して送る
        // Send a request to the registered CEF connection and await its terminal state; the body gains the request id
        public async UniTask<WebUiChannelResponse<TResult>> Send(WebSocketHub hub, string topic, object requestBody, float timeoutSeconds)
        {
            if (!WebUiCefConnection.TryGetConnection(hub, out var connectionId))
            {
                Debug.LogWarning("[PlaytestUi] No registered CEF connection");
                return new WebUiChannelResponse<TResult>(WebUiResponseState.Stopped, null, "No registered CEF connection");
            }
            if (!(0f < timeoutSeconds)) throw new TimeoutException("UI request deadline expired before delivery");
            var requestId = Open(connectionId, hub);
            // 日時の形をした文字列を書き換えないよう、日時解釈を切って読み直す
            // Reparse with date handling off so date-shaped strings are not rewritten
            var reader = new JsonTextReader(new StringReader(WebUiJson.Serialize(requestBody))) { DateParseHandling = DateParseHandling.None };
            var payload = JObject.Load(reader);
            payload["requestId"] = requestId;
            if (!hub.PublishTo(connectionId, topic, payload.ToString(Formatting.None)))
            {
                Stop(requestId);
                return new WebUiChannelResponse<TResult>(WebUiResponseState.Stopped, null, "CEF disconnected or not subscribed");
            }
            return await Await(requestId, timeoutSeconds);
        }

        public string Open(Guid connectionId, WebSocketHub hub)
        {
            var requestId = Guid.NewGuid().ToString();
            _pending.Add(requestId, new PendingRequest(connectionId, hub));
            return requestId;
        }

        // payloadの内容を拒否する前に実際の接続元を照合する
        // Check the actual sender before rejecting any payload contents
        public bool Accepts(string requestId, Guid connectionId)
        {
            if (_pending.TryGetValue(requestId, out var pending) && pending.ConnectionId == connectionId &&
                pending.Response.State == WebUiResponseState.Pending && pending.IsCurrentConnection()) return true;
            Debug.LogWarning($"[PlaytestUi] Rejected response from unrelated connection or completed request: {requestId}");
            return false;
        }

        public void Resolve(string requestId, TResult result)
        {
            _pending[requestId].Response = new WebUiChannelResponse<TResult>(WebUiResponseState.Responded, result, string.Empty);
        }

        public void Reject(string requestId, string reason)
        {
            _pending[requestId].Response = new WebUiChannelResponse<TResult>(WebUiResponseState.Rejected, null, reason);
        }

        public async UniTask<WebUiChannelResponse<TResult>> Await(string requestId, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (true)
            {
                // 停止と無応答を区別し、応答済み状態は一度だけ取り出す
                // Distinguish stopped sessions from silence and consume a terminal result once
                if (!_pending.TryGetValue(requestId, out var pending))
                    return new WebUiChannelResponse<TResult>(WebUiResponseState.Stopped, null, "UI session stopped");
                // 正規送信元が確定した拒否理由は、その後の期限・切断で失わせない
                // Preserve an authenticated rejection across later deadline or disconnect changes
                if (pending.Response.State == WebUiResponseState.Rejected)
                {
                    _pending.Remove(requestId);
                    return pending.Response;
                }
                if (deadline <= Time.realtimeSinceStartup)
                {
                    _pending.Remove(requestId);
                    return new WebUiChannelResponse<TResult>(WebUiResponseState.TimedOut, null, "Web response deadline expired");
                }
                if (!pending.IsCurrentConnection())
                {
                    _pending.Remove(requestId);
                    return new WebUiChannelResponse<TResult>(WebUiResponseState.Stopped, null, "CEF connection changed or disconnected");
                }
                if (pending.Response.State != WebUiResponseState.Pending)
                {
                    _pending.Remove(requestId);
                    return pending.Response;
                }
                await UniTask.Yield();
            }
        }

        public void Stop(string requestId) => _pending.Remove(requestId);

        public void Reset() => _pending.Clear();

        private sealed class PendingRequest
        {
            public readonly Guid ConnectionId;
            private readonly WebSocketHub _hub;
            public WebUiChannelResponse<TResult> Response;

            public PendingRequest(Guid connectionId, WebSocketHub hub)
            {
                ConnectionId = connectionId;
                _hub = hub;
                Response = new WebUiChannelResponse<TResult>(WebUiResponseState.Pending, null, string.Empty);
            }

            public bool IsCurrentConnection()
            {
                return WebUiCefConnection.TryGetConnection(_hub, out var current) && current == ConnectionId;
            }
        }
    }
}
