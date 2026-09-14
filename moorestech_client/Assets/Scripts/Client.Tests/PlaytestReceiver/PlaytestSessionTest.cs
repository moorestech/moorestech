using System;
using System.Collections.Generic;
using System.Threading;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Steam;
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

        private static void AssertOutcome(PlaytestApiResult response, PlaytestSessionOutcome expected)
        {
            var api = new FakeApi();
            api.SessionResponses.Add(response);
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));
            var result = session.AuthenticateAsync(DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(expected, result.Outcome, result.Detail);
        }

        private sealed class FakeTicketProvider : IPlaytestSteamTicketProvider
        {
            private readonly string _ticketHex;
            public FakeTicketProvider(string ticketHex) { _ticketHex = ticketHex; }
            public bool IsSteamRunning() { return true; }
            public UniTask<string> RequestWebApiTicketHexAsync(CancellationToken token) { return UniTask.FromResult(_ticketHex); }
        }

        private sealed class FakeApi : IPlaytestReceiverApi
        {
            public readonly List<PlaytestApiResult> SessionResponses = new();
            public int SessionCallCount;

            public UniTask<PlaytestApiResult> PostSessionAsync(string ticketHex, CancellationToken token)
            {
                var response = SessionResponses[SessionCallCount];
                SessionCallCount++;
                return UniTask.FromResult(response);
            }

            public UniTask<PlaytestApiResult> PutFileAsync(string bearerToken, string kind, string bundleId, string relativePath, string absoluteFilePath, CancellationToken token)
            {
                return UniTask.FromResult(new PlaytestApiResult { StatusCode = 200, Body = "{}" });
            }

            public UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, string kind, string bundleId, string summaryJson, CancellationToken token)
            {
                return UniTask.FromResult(new PlaytestApiResult { StatusCode = 200, Body = "{}" });
            }
        }
    }
}
