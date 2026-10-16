using System;
using System.Threading;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Credentials;
using Client.PlaytestReceiver.Http;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Credentials
{
    public class DeveloperTokenSessionCredentialTest
    {
        [TestCase("{\"steamId\":\"dev-macmini\"}", false)]
        [TestCase("{\"steamId\":\"dev-macmini\",\"token\":\"jwt\",\"expiresAt\":\"2999-01-01T00:00:00Z\"}", true)]
        public void 接続確認と実送信が同じセッション契約を使う(string body, bool valid)
        {
            var parsed = Client.PlaytestReceiver.Http.Responses.PlaytestSessionResponse.Parse(body);
            Assert.AreEqual(valid, parsed != null);
            if (valid) Assert.AreEqual("dev-macmini", parsed.SteamId);
            var api = new FakeApi();
            api.DeveloperSessionResponses.Add(PlaytestApiResult.Responded(200, body));
            var session = new PlaytestSession(api, new DeveloperTokenSessionCredential("mdt_" + new string('a', 64)));
            var result = session.AuthenticateAsync(DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(valid ? PlaytestSessionOutcome.Authenticated : PlaytestSessionOutcome.MalformedResponse, result.Outcome);
        }

        [TestCase(200, PlaytestSessionOutcome.Authenticated)]
        [TestCase(401, PlaytestSessionOutcome.TicketRejected)]
        public void 開発者資格情報を交換して結果を返す(int status, PlaytestSessionOutcome outcome)
        {
            var developerToken = "mdt_" + new string('a', 64);
            var api = new FakeApi();
            api.DeveloperSessionResponses.Add(PlaytestApiResult.Responded(status, PlaytestSessionBodies.AllowedFarFuture));
            var session = new PlaytestSession(api, new DeveloperTokenSessionCredential(developerToken));

            // Steam経路を通らず資格情報そのものを受け口へ渡す
            // Send the credential itself to the receiver without calling the Steam route
            var result = session.AuthenticateAsync(DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(outcome, result.Outcome);
            CollectionAssert.AreEqual(new[] { developerToken }, api.DeveloperTokens);
            Assert.AreEqual(0, api.SessionCallCount);
        }
    }
}
