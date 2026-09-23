using System;
using System.Threading;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Http.Responses;
using Client.PlaytestReceiver.Steam;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.PlaytestReceiver
{
    // Steamチケットと受け口トークンの保持者。トークンの寿命管理と401時の取り直しはここ1箇所
    // Holder of the Steam ticket exchange and the receiver token; token lifetime and the 401 refresh live here alone
    public sealed class PlaytestSession
    {
        private readonly IPlaytestReceiverApi _api;
        private readonly IPlaytestSteamTicketProvider _ticketProvider;

        private string _token;
        private DateTime _tokenRefreshAtUtc;
        private UniTaskCompletionSource<PlaytestSessionResult> _inFlight;

        public PlaytestSession(IPlaytestReceiverApi api, IPlaytestSteamTicketProvider ticketProvider)
        {
            _api = api;
            _ticketProvider = ticketProvider;
        }

        // 認証は常に1本。重なった呼び手は実行中の認証に相乗りし、同じ結果を受け取る
        // Authentication is single-flight; an overlapping caller joins the one in flight and receives the same result
        public async UniTask<PlaytestSessionResult> AuthenticateAsync(DateTime utcNow, CancellationToken token)
        {
            if (_inFlight != null) return await _inFlight.Task;

            var flight = new UniTaskCompletionSource<PlaytestSessionResult>();
            _inFlight = flight;

            // 打ち切り・例外でも相乗り側を宙に浮かせず、次の認証も塞がない
            // Even on cancellation or failure the joiners are released and the next authentication stays possible
            try
            {
                var result = await AuthenticateOnceAsync();
                flight.TrySetResult(result);
                return result;
            }
            finally
            {
                _inFlight = null;
                flight.TrySetCanceled();
            }

            #region Internal

            async UniTask<PlaytestSessionResult> AuthenticateOnceAsync()
            {
                var ticketHex = await _ticketProvider.RequestWebApiTicketHexAsync(token);
                if (ticketHex == null) return PlaytestSessionResult.Failed(PlaytestSessionOutcome.TicketUnavailable, "no web api ticket");

                // 受け口の検証が終わった直後に解放する。打ち切りで抜けても発行済みチケットを残さない
                // Released right after the receiver's verification; a cancelled request never leaves the issued ticket behind
                PlaytestApiResult response;
                try
                {
                    response = await _api.PostSessionAsync(ticketHex, token);
                }
                finally
                {
                    _ticketProvider.ReleaseWebApiTicket();
                }

                if (response.Kind != PlaytestApiResultKind.Responded) return PlaytestSessionResult.Failed(PlaytestSessionOutcome.Unreachable, response.Detail);

                // 状態コードの意味は受け口の契約そのまま。503（Steam・許可リストの障害）は到達不能と同じく止める
                // Status codes carry the receiver's contract verbatim; a 503 (Steam or allowlist outage) stops like unreachability
                if (response.StatusCode == 403) return PlaytestSessionResult.Failed(PlaytestSessionOutcome.NotAllowed, response.Body);
                if (response.StatusCode == 401) return PlaytestSessionResult.Failed(PlaytestSessionOutcome.TicketRejected, response.Body);
                if (response.StatusCode != 200) return PlaytestSessionResult.Failed(PlaytestSessionOutcome.Unreachable, $"HTTP {response.StatusCode} {response.Body}");

                // 200でも本文は外部入力。形が違えば契約違反として返し、トークン無しでAllowedを返さない
                // Even a 200 body is external input; a malformed one comes back as a contract breach, never as Allowed
                var parsed = PlaytestSessionResponse.Parse(response.Body);
                if (parsed == null) return PlaytestSessionResult.Failed(PlaytestSessionOutcome.MalformedResponse, "malformed session response");

                if (!parsed.Allowed)
                {
                    Debug.LogWarning("[PlaytestReceiver] session answered 200 without allowed; treating it as not allowed");
                    return PlaytestSessionResult.Failed(PlaytestSessionOutcome.NotAllowed, "200 without allowed");
                }

                // 更新時刻は受け口が名乗った期限から逆算する。寿命の正本を受け口1箇所に保つ
                // The refresh time is derived from the expiry the receiver states, keeping the lifetime's source there alone
                _token = parsed.Token;
                _tokenRefreshAtUtc = parsed.ExpiresAtUtc.AddSeconds(-PlaytestReceiverConfig.TokenRefreshMarginSeconds);

                // 検証済みSteamIDは結末に載せて渡す。セッションに残すと、後の再認証が失敗しても前回の値が読めてしまう（ADR 0065）
                // The verified SteamID rides on the outcome; keeping it on the session would let a later failed re-authentication still read the old value (ADR 0065)
                return PlaytestSessionResult.Allowed(parsed.SteamId);
            }

            #endregion
        }

        // 期限が近ければ取り直す。取り直せなければnull
        // Refreshes near expiry; returns null when the refresh fails
        public async UniTask<string> GetValidTokenAsync(DateTime utcNow, CancellationToken token)
        {
            var ensured = await EnsureTokenAsync(utcNow, false, token);
            return ensured.IsUsable ? _token : null;
        }

        // 認可付きの呼び出し。401は期限切れとして1回だけ取り直して再送し、トークンが取れなければ理由付きで返す
        // An authorized call; a 401 is treated as expiry and retried once, and a token failure comes back with its outcome
        internal async UniTask<PlaytestApiResult> SendAuthorizedAsync(IPlaytestAuthorizedCall call, CancellationToken token)
        {
            var ensured = await EnsureTokenAsync(DateTime.UtcNow, false, token);
            if (!ensured.IsUsable) return PlaytestApiResult.SessionUnavailable(ensured.FailureOutcome, $"{ensured.FailureOutcome} {ensured.Detail}");

            var response = await call.SendAsync(_token, token);
            if (response.Kind != PlaytestApiResultKind.Responded || response.StatusCode != 401) return response;

            Debug.Log("[PlaytestReceiver] the receiver rejected the token; renewing it once and retrying");
            var renewed = await EnsureTokenAsync(DateTime.UtcNow, true, token);
            if (!renewed.IsUsable) return PlaytestApiResult.SessionUnavailable(renewed.FailureOutcome, $"{renewed.FailureOutcome} {renewed.Detail}");

            return await call.SendAsync(_token, token);
        }

        // キャッシュ命中は認証を経ないのでSteamIDを持たない。認証の結末を装わずトークンの可用性だけを返す
        // A cache hit skips authentication and has no SteamID, so it reports token availability instead of posing as an authentication outcome
        private async UniTask<PlaytestTokenAvailability> EnsureTokenAsync(DateTime utcNow, bool forceRenew, CancellationToken token)
        {
            if (!forceRenew && _token != null && utcNow < _tokenRefreshAtUtc) return PlaytestTokenAvailability.Usable;

            var result = await AuthenticateAsync(utcNow, token);
            if (result.Outcome == PlaytestSessionOutcome.Allowed) return PlaytestTokenAvailability.Usable;

            _token = null;
            Debug.LogWarning($"[PlaytestReceiver] could not refresh the session token: {result.Outcome} {result.Detail}");
            return PlaytestTokenAvailability.Unavailable(result);
        }
    }
}
