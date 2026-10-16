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
