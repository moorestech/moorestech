using System;
using System.Threading;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Steam;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.PlaytestReceiver
{
    public enum PlaytestSessionOutcome
    {
        Allowed,
        NotAllowed,
        TicketRejected,
        TicketUnavailable,
        Unreachable,
    }

    // 認証1回の結末。Outcomeで分岐、Detailはログ専用
    // The outcome of one authentication; callers branch on Outcome and Detail only feeds developer logs
    public sealed class PlaytestSessionResult
    {
        public PlaytestSessionOutcome Outcome;
        public string SteamId;
        public string Detail;
    }

    // Steamチケットと受け口トークンの保持者。トークンの寿命管理はここ1箇所
    // Holder of the Steam ticket exchange and the receiver token; token lifetime lives here alone
    public sealed class PlaytestSession
    {
        private readonly IPlaytestReceiverApi _api;
        private readonly IPlaytestSteamTicketProvider _ticketProvider;

        private string _token;
        private DateTime _tokenIssuedAtUtc;
        private bool _authenticating;

        public PlaytestSession(IPlaytestReceiverApi api, IPlaytestSteamTicketProvider ticketProvider)
        {
            _api = api;
            _ticketProvider = ticketProvider;
        }

        public string SteamId { get; private set; }
        public bool HasToken => _token != null;

        // 認証は常に1本。重ねて呼ばれたらチケット取得失敗と区別できるDetailで断る（チケット待ちは重ねられない）
        // Authentication is single-flight; an overlapping call is refused with a Detail that is not a ticket failure
        public async UniTask<PlaytestSessionResult> AuthenticateAsync(DateTime utcNow, CancellationToken token)
        {
            if (_authenticating)
            {
                Debug.LogWarning("[PlaytestReceiver] refused an authentication while another one is in flight");
                return new PlaytestSessionResult { Outcome = PlaytestSessionOutcome.TicketUnavailable, Detail = "another authentication is already in flight" };
            }

            _authenticating = true;

            // 打ち切り後も再認証できるよう、走行フラグは例外経路でも必ず戻す
            // The in-flight flag is always restored, even on the cancellation path, so authentication can be retried
            try
            {
                return await AuthenticateOnceAsync(utcNow, token);
            }
            finally
            {
                _authenticating = false;
            }
        }

        private async UniTask<PlaytestSessionResult> AuthenticateOnceAsync(DateTime utcNow, CancellationToken token)
        {
            var ticketHex = await _ticketProvider.RequestWebApiTicketHexAsync(token);
            if (ticketHex == null)
            {
                return new PlaytestSessionResult { Outcome = PlaytestSessionOutcome.TicketUnavailable, Detail = "no web api ticket" };
            }

            var response = await _api.PostSessionAsync(ticketHex, token);

            // 受け口の検証が終わった直後に解放する。検証前に取り消すとSteam側でチケットが無効になる
            // Released right after the receiver's verification finishes; cancelling earlier invalidates the ticket on Steam's side
            _ticketProvider.ReleaseWebApiTicket();

            if (response.IsTransportFailure)
            {
                return new PlaytestSessionResult { Outcome = PlaytestSessionOutcome.Unreachable, Detail = response.TransportError };
            }

            // 状態コードの意味は受け口の契約（§4）そのまま。ここが唯一の対応表
            // Status codes carry the receiver's contract (§4) verbatim; this is the single mapping table
            if (response.StatusCode == 403) return new PlaytestSessionResult { Outcome = PlaytestSessionOutcome.NotAllowed, Detail = response.Body };
            if (response.StatusCode == 401) return new PlaytestSessionResult { Outcome = PlaytestSessionOutcome.TicketRejected, Detail = response.Body };
            if (response.StatusCode != 200) return new PlaytestSessionResult { Outcome = PlaytestSessionOutcome.Unreachable, Detail = $"HTTP {response.StatusCode}" };

            // 200でも本文は外部入力。形が違えば到達できなかったのと同じ扱いにし、トークン無しでAllowedを返さない
            // Even a 200 body is external input; a malformed one counts as not reaching the receiver, never as Allowed
            var parsed = PlaytestSessionResponse.Parse(response.Body);
            if (parsed == null)
            {
                return new PlaytestSessionResult { Outcome = PlaytestSessionOutcome.Unreachable, Detail = "malformed session response" };
            }

            // 受け口は拒否を403で返す。200でallowedが立っていないのは契約違反なので、拒否側へ倒す
            // The receiver denies with 403, so a 200 without allowed breaks the contract and falls to the denying side
            if (!parsed.Allowed)
            {
                Debug.LogWarning("[PlaytestReceiver] session answered 200 without allowed; treating it as not allowed");
                return new PlaytestSessionResult { Outcome = PlaytestSessionOutcome.NotAllowed, Detail = "200 without allowed" };
            }

            SteamId = parsed.SteamId;
            _token = parsed.Token;
            _tokenIssuedAtUtc = utcNow;
            return new PlaytestSessionResult { Outcome = PlaytestSessionOutcome.Allowed, SteamId = SteamId };
        }

        // 送信の直前に呼ぶ。45分を超えていたら取り直し、取り直せなければnullを返して呼び出し側が持ち越す
        // Called right before an upload; refreshes past 45 minutes and returns null so the caller defers on failure
        public async UniTask<string> GetValidTokenAsync(DateTime utcNow, CancellationToken token)
        {
            var age = utcNow - _tokenIssuedAtUtc;
            if (_token != null && age.TotalSeconds < PlaytestReceiverConfig.TokenRefreshAfterSeconds) return _token;

            return await RenewTokenAsync(utcNow, token);
        }

        // 年齢を見ずに強制的に取り直す。401等で「今のトークンは既に無効」と分かっている呼び出し専用
        // Forces a fresh authentication regardless of age; for callers that already know the current token is invalid (e.g. after a 401)
        public async UniTask<string> RenewTokenAsync(DateTime utcNow, CancellationToken token)
        {
            var result = await AuthenticateAsync(utcNow, token);
            if (result.Outcome == PlaytestSessionOutcome.Allowed) return _token;

            _token = null;
            Debug.LogWarning($"[PlaytestReceiver] could not refresh the session token: {result.Outcome} {result.Detail}");
            return null;
        }
    }
}
