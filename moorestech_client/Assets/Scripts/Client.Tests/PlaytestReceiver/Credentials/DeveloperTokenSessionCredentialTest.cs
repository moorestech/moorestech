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
            if (valid) Assert.AreEqual("dev-macmini", ((PlaytestSessionAuthenticated)result).Response.SteamId);
            else Assert.AreEqual(PlaytestSessionOutcome.MalformedResponse, ((PlaytestSessionFailed)result).Outcome);
        }

        [TestCase("7656", false)]
        [TestCase("dev-macmini", true)]
        public void 資格情報ごとのsubjectを実認証で検証する(string subject, bool developer)
        {
            var body = PlaytestSessionBodies.AllowedFarFuture.Replace("7656", subject);
            var api = new FakeApi();
            api.DeveloperSessionResponses.Add(PlaytestApiResult.Responded(200, body));
            api.SessionResponses.Add(PlaytestApiResult.Responded(200, body));
            var dev = new PlaytestSession(api, new DeveloperTokenSessionCredential("token"));
            var steam = new PlaytestSession(api, new SteamTicketSessionCredential(new FakeTicketProvider("aabb")));
            var devResult = dev.AuthenticateAsync(DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();
            var steamResult = steam.AuthenticateAsync(DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(developer, devResult is PlaytestSessionAuthenticated);
            Assert.AreEqual(!developer, steamResult is PlaytestSessionAuthenticated);
            var rejected = (PlaytestSessionFailed)(developer ? steamResult : devResult);
            Assert.AreEqual(PlaytestSessionOutcome.MalformedResponse, rejected.Outcome);
        }

        [TestCase(200, PlaytestSessionOutcome.Authenticated)]
        [TestCase(401, PlaytestSessionOutcome.TicketRejected)]
        public void 開発者資格情報を交換して結果を返す(int status, PlaytestSessionOutcome outcome)
        {
            var developerToken = "mdt_" + new string('a', 64);
            var api = new FakeApi();
            api.DeveloperSessionResponses.Add(PlaytestApiResult.Responded(status, PlaytestSessionBodies.AllowedFarFuture.Replace("7656", "dev-macmini")));
            var session = new PlaytestSession(api, new DeveloperTokenSessionCredential(developerToken));

            // Steamを介さず資格情報を渡す
            // Send credentials without using the Steam route
            var result = session.AuthenticateAsync(DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();
            if (outcome == PlaytestSessionOutcome.Authenticated) Assert.IsInstanceOf<PlaytestSessionAuthenticated>(result);
            else Assert.AreEqual(outcome, ((PlaytestSessionFailed)result).Outcome);
            CollectionAssert.AreEqual(new[] { developerToken }, api.DeveloperTokens);
            Assert.AreEqual(0, api.SessionCallCount);
        }
    }
}
