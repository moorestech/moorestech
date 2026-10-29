using System.Collections;
using System.Text.RegularExpressions;
using Client.Playtest.WebUi;
using Client.WebUiHost.Cef;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Playtest.UiMapping
{
    public class PlaytestRejectedBoundaryTest
    {
        [UnityTest]
        public IEnumerator RejectedBeforeDeadlinePreservesReasonAfterDeadline()
        {
            using var fixture = new PlaytestConnectionFixture();
            var channel = new PlaytestWebUiRequestChannel<DomSetTextResult>();
            var id = channel.Open(fixture.CefId, fixture.Hub);
            var awaiting = channel.Await(id, 0.05f).GetAwaiter();
            Reject(fixture, channel, id);
            // PlayerLoop再開前に実時間の期限だけを越える
            // Cross the real deadline before allowing the PlayerLoop continuation
            System.Threading.Thread.Sleep(100);
            while (!awaiting.IsCompleted) yield return null;
            var response = awaiting.GetResult();
            Assert.AreEqual(WebUiResponseState.Rejected, response.State);
            StringAssert.Contains("Malformed", response.Reason);
        }

        [UnityTest]
        public IEnumerator RejectedBeforeDisconnectPreservesReasonAfterDisconnect()
        {
            using var fixture = new PlaytestConnectionFixture();
            var channel = new PlaytestWebUiRequestChannel<DomSetTextResult>();
            var id = channel.Open(fixture.CefId, fixture.Hub);
            var awaiting = channel.Await(id, 5f).GetAwaiter();
            Reject(fixture, channel, id);
            WebUiCefConnection.Release(fixture.Hub, fixture.CefId);
            while (!awaiting.IsCompleted) yield return null;
            var response = awaiting.GetResult();
            Assert.AreEqual(WebUiResponseState.Rejected, response.State);
            StringAssert.Contains("Malformed", response.Reason);
        }

        [UnityTest]
        public IEnumerator SuccessfulResponseAfterDeadlineStillTimesOut()
        {
            using var fixture = new PlaytestConnectionFixture();
            var channel = new PlaytestWebUiRequestChannel<DomSetTextResult>();
            var id = channel.Open(fixture.CefId, fixture.Hub);
            var awaiting = channel.Await(id, 0.05f).GetAwaiter();
            Assert.IsTrue(channel.Accepts(id, fixture.CefId));
            channel.Resolve(id, new DomSetTextResult());
            System.Threading.Thread.Sleep(100);
            while (!awaiting.IsCompleted) yield return null;
            Assert.AreEqual(WebUiResponseState.TimedOut, awaiting.GetResult().State);
        }

        [UnityTest]
        public IEnumerator SuccessfulResponseAfterDisconnectIsStillStopped()
        {
            using var fixture = new PlaytestConnectionFixture();
            var channel = new PlaytestWebUiRequestChannel<DomSetTextResult>();
            var id = channel.Open(fixture.CefId, fixture.Hub);
            var awaiting = channel.Await(id, 5f).GetAwaiter();
            Assert.IsTrue(channel.Accepts(id, fixture.CefId));
            channel.Resolve(id, new DomSetTextResult());
            WebUiCefConnection.Release(fixture.Hub, fixture.CefId);
            while (!awaiting.IsCompleted) yield return null;
            Assert.AreEqual(WebUiResponseState.Stopped, awaiting.GetResult().State);
        }

        [UnityTest]
        public IEnumerator SessionResetStillStopsPreviouslyRejectedRequest()
        {
            using var fixture = new PlaytestConnectionFixture();
            var channel = new PlaytestWebUiRequestChannel<DomSetTextResult>();
            var id = channel.Open(fixture.CefId, fixture.Hub);
            var awaiting = channel.Await(id, 5f).GetAwaiter();
            Reject(fixture, channel, id);
            channel.Reset();
            while (!awaiting.IsCompleted) yield return null;
            Assert.AreEqual(WebUiResponseState.Stopped, awaiting.GetResult().State);
        }

        private static void Reject(PlaytestConnectionFixture fixture, PlaytestWebUiRequestChannel<DomSetTextResult> channel, string id)
        {
            // 正規接続のhandlerで検証し、不正payloadを拒否させる
            // Validate through the legitimate sender handler and reject its malformed payload
            var handler = new DomSetTextResultActionHandler(channel);
            LogAssert.Expect(LogType.Warning, new Regex("malformed playtest.dom_set_text_result"));
            var payload = new JObject { ["requestId"] = id, ["status"] = "typed" };
            Assert.AreEqual("invalid_payload", handler.ExecuteAsync(fixture.CefId, payload).GetAwaiter().GetResult().Error);
        }
    }
}
