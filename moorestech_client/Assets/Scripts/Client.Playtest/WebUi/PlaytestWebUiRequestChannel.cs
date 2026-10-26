using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.WebUi
{
    /// <summary>
    ///     ・要求ID単位の保留
    ///     ・action応答による解決
    ///     ・Pending requests keyed by request ID
    ///     ・Resolution by action responses
    /// </summary>
    internal class PlaytestWebUiRequestChannel<TResult> where TResult : class
    {
        private readonly Dictionary<string, TResult> _pending = new();

        public string Open()
        {
            // 配信前に保留登録し、即時応答も取りこぼさない
            // Register as pending before publishing so even an immediate response is retained
            var requestId = Guid.NewGuid().ToString();
            _pending[requestId] = null;
            return requestId;
        }

        public void Resolve(string requestId, TResult result)
        {
            // タイムアウト済みの遅延応答は捨て、待機中のrequestIdだけを解決する
            // Drop late responses after timeout and resolve only requestIds still pending
            if (_pending.ContainsKey(requestId)) _pending[requestId] = result;
        }

        public async UniTask<WebUiChannelResponse<TResult>> Await(string requestId, float timeoutSeconds)
        {
            // 毎フレーム待機し期限切れは応答なし
            // Wait per frame and return no response on timeout
            var startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt <= timeoutSeconds)
            {
                // Resetで保留が消えたら、待ち続けず応答なしとして返す
                // When Reset dropped the pending entry, stop waiting and report no response
                if (!_pending.TryGetValue(requestId, out var result)) return WebUiChannelResponse<TResult>.TimedOut();
                if (result != null)
                {
                    _pending.Remove(requestId);
                    return WebUiChannelResponse<TResult>.Responded(result);
                }
                await UniTask.Yield();
            }
            _pending.Remove(requestId);
            return WebUiChannelResponse<TResult>.TimedOut();
        }

        public void Reset()
        {
            _pending.Clear();
        }
    }

    internal readonly struct WebUiChannelResponse<TResult> where TResult : class
    {
        public readonly bool HasResponse;
        public readonly TResult Result;

        private WebUiChannelResponse(bool hasResponse, TResult result)
        {
            HasResponse = hasResponse;
            Result = result;
        }

        public static WebUiChannelResponse<TResult> Responded(TResult result)
        {
            return new WebUiChannelResponse<TResult>(true, result);
        }

        public static WebUiChannelResponse<TResult> TimedOut()
        {
            return new WebUiChannelResponse<TResult>(false, null);
        }
    }
}
