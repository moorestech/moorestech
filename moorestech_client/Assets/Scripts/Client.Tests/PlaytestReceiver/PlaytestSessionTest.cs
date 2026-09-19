using System;
using System.Threading;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Http;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestSessionTest
    {
        private static readonly DateTime IssuedAt = new(2026, 9, 13, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void 許可されればトークンを保持する()
        {
            var api = ApiAnswering(PlaytestSessionBodies.Allowed("tok-1", "2026-09-13T01:00:00.000Z"));
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));

            var result = Authenticate(session, IssuedAt);

            Assert.AreEqual(PlaytestSessionOutcome.Allowed, result.Outcome);
            Assert.AreEqual("tok-1", session.GetValidTokenAsync(IssuedAt, CancellationToken.None).GetAwaiter().GetResult());
            Assert.AreEqual(1, api.SessionCallCount);
            Assert.AreEqual("7656", session.VerifiedSteamId, "受け口が検証したSteamIDを保持していない");
        }

        [Test]
        public void 受け口の期限より余裕を持って取り直す()
        {
            // 更新時刻は受け口が名乗った期限から逆算する。クライアント側にトークン寿命の複製を持たない
            // The refresh time is derived from the stated expiry; the client keeps no copy of the token lifetime
            var api = ApiAnswering(PlaytestSessionBodies.Allowed("tok-1", "2026-09-13T01:00:00.000Z"), PlaytestSessionBodies.Allowed("tok-2", "2026-09-13T02:00:00.000Z"));
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));
            Authenticate(session, IssuedAt);
            var refreshAt = IssuedAt.AddHours(1).AddSeconds(-PlaytestReceiverConfig.TokenRefreshMarginSeconds);

            Assert.AreEqual("tok-1", session.GetValidTokenAsync(refreshAt.AddSeconds(-1), CancellationToken.None).GetAwaiter().GetResult());
            Assert.AreEqual(1, api.SessionCallCount);
            Assert.AreEqual("tok-2", session.GetValidTokenAsync(refreshAt.AddSeconds(1), CancellationToken.None).GetAwaiter().GetResult());
            Assert.AreEqual(2, api.SessionCallCount);
        }

        [Test]
        public void 応答コードごとに結末が分かれる()
        {
            AssertOutcome(PlaytestApiResult.Responded(403, "{\"reason\":\"not-allowed\"}"), PlaytestSessionOutcome.NotAllowed);
            AssertOutcome(PlaytestApiResult.Responded(401, "{\"reason\":\"invalid-ticket\"}"), PlaytestSessionOutcome.TicketRejected);
            AssertOutcome(PlaytestApiResult.Responded(503, "{\"reason\":\"steam-unavailable\"}"), PlaytestSessionOutcome.Unreachable);
            AssertOutcome(PlaytestApiResult.Responded(500, ""), PlaytestSessionOutcome.Unreachable);
            AssertOutcome(PlaytestApiResult.TransportFailure("name resolution failed"), PlaytestSessionOutcome.Unreachable);
        }

        [Test]
        public void 形の欠けた200では許可しない()
        {
            // トークン・期限・steamIdの欠落、空や空白のsteamId・token、JSONでない本文（キャプティブポータル）はいずれも到達不能と混ぜず契約違反として返す
            // A missing token, expiry or steamId, an empty or whitespace-only steamId/token, or a non-JSON body (captive portal) all come back as a contract breach, not as unreachability
            AssertOutcome(PlaytestApiResult.Responded(200, "{\"steamId\":\"7656\",\"allowed\":true,\"expiresAt\":\"2999-01-01T00:00:00Z\"}"), PlaytestSessionOutcome.MalformedResponse);
            AssertOutcome(PlaytestApiResult.Responded(200, "{\"steamId\":\"7656\",\"allowed\":true,\"token\":\"tok-1\"}"), PlaytestSessionOutcome.MalformedResponse);
            AssertOutcome(PlaytestApiResult.Responded(200, "{\"allowed\":true,\"token\":\"tok-1\",\"expiresAt\":\"2999-01-01T00:00:00Z\"}"), PlaytestSessionOutcome.MalformedResponse);
            AssertOutcome(PlaytestApiResult.Responded(200, "{\"steamId\":\"\",\"allowed\":true,\"token\":\"tok-1\",\"expiresAt\":\"2999-01-01T00:00:00Z\"}"), PlaytestSessionOutcome.MalformedResponse);
            AssertOutcome(PlaytestApiResult.Responded(200, "{\"steamId\":\"  \",\"allowed\":true,\"token\":\"tok-1\",\"expiresAt\":\"2999-01-01T00:00:00Z\"}"), PlaytestSessionOutcome.MalformedResponse);
            AssertOutcome(PlaytestApiResult.Responded(200, "{\"steamId\":\"7656\",\"allowed\":true,\"token\":\"  \",\"expiresAt\":\"2999-01-01T00:00:00Z\"}"), PlaytestSessionOutcome.MalformedResponse);
            AssertOutcome(PlaytestApiResult.Responded(200, "<html>sign in to the wifi</html>"), PlaytestSessionOutcome.MalformedResponse);
        }

        [Test]
        public void allowedが立っていない200では許可しない()
        {
            AssertOutcome(PlaytestApiResult.Responded(200, "{\"steamId\":\"7656\",\"allowed\":false,\"token\":\"tok-1\",\"expiresAt\":\"2999-01-01T00:00:00Z\"}"), PlaytestSessionOutcome.NotAllowed);
        }

        [Test]
        public void チケットが取れなければ受け口を叩かない()
        {
            var api = new FakeApi();
            var session = new PlaytestSession(api, new FakeTicketProvider(null));

            Assert.AreEqual(PlaytestSessionOutcome.TicketUnavailable, Authenticate(session, IssuedAt).Outcome);
            Assert.AreEqual(0, api.SessionCallCount);
        }

        [Test]
        public void 認証中に重なった呼び出しは実行中の認証に相乗りする()
        {
            var api = ApiAnswering(PlaytestSessionBodies.AllowedFarFuture);
            var gate = new UniTaskCompletionSource<string>();
            var session = new PlaytestSession(api, new GatedTicketProvider(gate));

            var first = session.AuthenticateAsync(IssuedAt, CancellationToken.None);
            var second = session.AuthenticateAsync(IssuedAt, CancellationToken.None);
            gate.TrySetResult("aabb");

            Assert.AreEqual(PlaytestSessionOutcome.Allowed, first.GetAwaiter().GetResult().Outcome);
            Assert.AreEqual(PlaytestSessionOutcome.Allowed, second.GetAwaiter().GetResult().Outcome);
            Assert.AreEqual(1, api.SessionCallCount);
        }

        [Test]
        public void 打ち切られた後も認証をやり直せチケットは解放される()
        {
            // 走行フラグが例外経路で残ると以後の認証が恒久に拒否され、解放漏れはSteam側にチケットを溜める
            // A flag left on the exception path would refuse every later authentication, and a missed release piles tickets up on Steam
            var api = ApiAnswering(PlaytestSessionBodies.AllowedFarFuture);
            var session = new PlaytestSession(api, new TrackingTicketProvider("aabb", api.Events));
            var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            Assert.Catch<OperationCanceledException>(() => session.AuthenticateAsync(IssuedAt, cancelled.Token).GetAwaiter().GetResult());
            CollectionAssert.AreEqual(new[] { "release" }, api.Events);

            Assert.AreEqual(PlaytestSessionOutcome.Allowed, Authenticate(session, IssuedAt).Outcome);
        }

        [Test]
        public void チケットは受け口の検証後に解放される()
        {
            // 検証前に解放するとSteam側でチケットが無効になり、受け口が401で拒否する
            // Releasing before verification invalidates the ticket on Steam's side, so the receiver would answer 401
            var api = ApiAnswering(PlaytestSessionBodies.AllowedFarFuture);
            var session = new PlaytestSession(api, new TrackingTicketProvider("aabb", api.Events));

            Authenticate(session, IssuedAt);

            CollectionAssert.AreEqual(new[] { "post-session", "release" }, api.Events);
        }

        private static FakeApi ApiAnswering(params string[] bodies)
        {
            var api = new FakeApi();
            foreach (var body in bodies) api.SessionResponses.Add(PlaytestApiResult.Responded(200, body));
            return api;
        }

        private static PlaytestSessionResult Authenticate(PlaytestSession session, DateTime utcNow)
        {
            return session.AuthenticateAsync(utcNow, CancellationToken.None).GetAwaiter().GetResult();
        }

        private static void AssertOutcome(PlaytestApiResult response, PlaytestSessionOutcome expected)
        {
            var api = new FakeApi();
            api.SessionResponses.Add(response);
            var result = Authenticate(new PlaytestSession(api, new FakeTicketProvider("aabb")), IssuedAt);
            Assert.AreEqual(expected, result.Outcome, result.Detail);
        }
    }
}
