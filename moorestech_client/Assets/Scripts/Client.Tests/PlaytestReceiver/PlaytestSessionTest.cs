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
        [Test]
        public void 許可されればトークンを保持しSteamIdが読める()
        {
            var api = new FakeApi();
            api.SessionResponses.Add(new PlaytestApiResult { StatusCode = 200, Body = "{\"steamId\":\"7656\",\"allowed\":true,\"token\":\"tok-1\"}" });
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));

            var result = session.AuthenticateAsync(new DateTime(2026, 9, 13, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(PlaytestSessionOutcome.Allowed, result.Outcome);
            Assert.AreEqual("7656", session.SteamId);
            Assert.IsTrue(session.HasToken);
        }

        [Test]
        public void 期限内はトークンを取り直さない()
        {
            var api = new FakeApi();
            api.SessionResponses.Add(new PlaytestApiResult { StatusCode = 200, Body = "{\"steamId\":\"7656\",\"allowed\":true,\"token\":\"tok-1\"}" });
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));
            var issuedAt = new DateTime(2026, 9, 13, 0, 0, 0, DateTimeKind.Utc);
            session.AuthenticateAsync(issuedAt, CancellationToken.None).GetAwaiter().GetResult();

            var token = session.GetValidTokenAsync(issuedAt.AddSeconds(PlaytestReceiverConfig.TokenRefreshAfterSeconds - 1), CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual("tok-1", token);
            Assert.AreEqual(1, api.SessionCallCount);
        }

        [Test]
        public void 期限が近づいたらトークンを取り直す()
        {
            var api = new FakeApi();
            api.SessionResponses.Add(new PlaytestApiResult { StatusCode = 200, Body = "{\"steamId\":\"7656\",\"allowed\":true,\"token\":\"tok-1\"}" });
            api.SessionResponses.Add(new PlaytestApiResult { StatusCode = 200, Body = "{\"steamId\":\"7656\",\"allowed\":true,\"token\":\"tok-2\"}" });
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));
            var issuedAt = new DateTime(2026, 9, 13, 0, 0, 0, DateTimeKind.Utc);
            session.AuthenticateAsync(issuedAt, CancellationToken.None).GetAwaiter().GetResult();

            var token = session.GetValidTokenAsync(issuedAt.AddSeconds(PlaytestReceiverConfig.TokenRefreshAfterSeconds + 1), CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual("tok-2", token);
            Assert.AreEqual(2, api.SessionCallCount);
        }

        [Test]
        public void 応答コードごとに結末が分かれる()
        {
            AssertOutcome(new PlaytestApiResult { StatusCode = 403, Body = "{\"reason\":\"not-allowed\"}" }, PlaytestSessionOutcome.NotAllowed);
            AssertOutcome(new PlaytestApiResult { StatusCode = 401, Body = "{\"reason\":\"invalid-ticket\"}" }, PlaytestSessionOutcome.TicketRejected);
            AssertOutcome(new PlaytestApiResult { StatusCode = 500, Body = "" }, PlaytestSessionOutcome.Unreachable);
            AssertOutcome(new PlaytestApiResult { TransportError = "name resolution failed" }, PlaytestSessionOutcome.Unreachable);
        }

        [Test]
        public void トークンの無い200では許可しない()
        {
            var api = new FakeApi();
            api.SessionResponses.Add(new PlaytestApiResult { StatusCode = 200, Body = "{\"steamId\":\"7656\",\"allowed\":true}" });
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));

            var result = session.AuthenticateAsync(DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(PlaytestSessionOutcome.Unreachable, result.Outcome);
            Assert.IsFalse(session.HasToken);
        }

        [Test]
        public void JSONでない200では許可しない()
        {
            // キャプティブポータルは200でHTMLを返す。例外を外へ漏らさずUnreachableへ畳む
            // A captive portal answers 200 with HTML; that must fold into Unreachable instead of throwing out
            var api = new FakeApi();
            api.SessionResponses.Add(new PlaytestApiResult { StatusCode = 200, Body = "<html>sign in to the wifi</html>" });
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));

            var result = session.AuthenticateAsync(DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(PlaytestSessionOutcome.Unreachable, result.Outcome);
            Assert.IsFalse(session.HasToken);
        }

        [Test]
        public void allowedが立っていない200では許可しない()
        {
            var api = new FakeApi();
            api.SessionResponses.Add(new PlaytestApiResult { StatusCode = 200, Body = "{\"steamId\":\"7656\",\"allowed\":false,\"token\":\"tok-1\"}" });
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));

            var result = session.AuthenticateAsync(DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(PlaytestSessionOutcome.NotAllowed, result.Outcome);
            Assert.IsFalse(session.HasToken);
        }

        [Test]
        public void チケットが取れなければ受け口を叩かない()
        {
            var api = new FakeApi();
            var session = new PlaytestSession(api, new FakeTicketProvider(null));

            var result = session.AuthenticateAsync(DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(PlaytestSessionOutcome.TicketUnavailable, result.Outcome);
            Assert.AreEqual(0, api.SessionCallCount);
        }

        [Test]
        public void 認証中の二重呼び出しはチケット失敗と区別できる()
        {
            var api = new FakeApi();
            api.SessionResponses.Add(new PlaytestApiResult { StatusCode = 200, Body = "{\"steamId\":\"7656\",\"allowed\":true,\"token\":\"tok-1\"}" });
            var gate = new UniTaskCompletionSource<string>();
            var session = new PlaytestSession(api, new GatedTicketProvider(gate));

            var first = session.AuthenticateAsync(DateTime.UtcNow, CancellationToken.None);
            var second = session.AuthenticateAsync(DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(PlaytestSessionOutcome.TicketUnavailable, second.Outcome);
            Assert.AreEqual("another authentication is already in flight", second.Detail);
            Assert.AreEqual(0, api.SessionCallCount);

            gate.TrySetResult("aabb");
            Assert.AreEqual(PlaytestSessionOutcome.Allowed, first.GetAwaiter().GetResult().Outcome);
        }

        [Test]
        public void 打ち切られた後も認証をやり直せる()
        {
            // 走行フラグが例外経路で立ったままだと、以後の認証が恒久的に拒否される
            // A flag left standing on the exception path would refuse every later authentication forever
            var api = new FakeApi();
            api.SessionResponses.Add(new PlaytestApiResult { StatusCode = 200, Body = "{\"steamId\":\"7656\",\"allowed\":true,\"token\":\"tok-1\"}" });
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));
            var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            Assert.Catch<OperationCanceledException>(() => session.AuthenticateAsync(DateTime.UtcNow, cancelled.Token).GetAwaiter().GetResult());
            var result = session.AuthenticateAsync(DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(PlaytestSessionOutcome.Allowed, result.Outcome);
            Assert.IsTrue(session.HasToken);
        }

        [Test]
        public void チケットは受け口の検証後に解放される()
        {
            // 検証前に解放するとSteam側でチケットが無効になり、受け口が401で拒否する
            // Releasing before verification invalidates the ticket on Steam's side, so the receiver would answer 401
            var api = new FakeApi();
            api.SessionResponses.Add(new PlaytestApiResult { StatusCode = 200, Body = "{\"steamId\":\"7656\",\"allowed\":true,\"token\":\"tok-1\"}" });
            var session = new PlaytestSession(api, new TrackingTicketProvider("aabb", api.Events));

            session.AuthenticateAsync(DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "post-session", "release" }, api.Events);
        }

        private static void AssertOutcome(PlaytestApiResult response, PlaytestSessionOutcome expected)
        {
            var api = new FakeApi();
            api.SessionResponses.Add(response);
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));
            var result = session.AuthenticateAsync(DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(expected, result.Outcome, result.Detail);
        }
    }
}
