using System;
using System.Collections;
using System.Text.RegularExpressions;
using Client.Playtest.Operations;
using Client.Playtest.UiElements;
using Client.Playtest.WebUi;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Playtest.UiMapping
{
    public class PlaytestRequestLifetimeTest
    {
        [UnityTest]
        public IEnumerator ShortBudgetDoesNotWaitForThreeSecondTransportCap()
        {
            using var fixture = new PlaytestConnectionFixture();
            var locator = new WebUiElementLocator();
            locator.StartSession();
            LogAssert.Expect(LogType.Warning, new Regex("Web UI gave no lookup answer"));
            var start = Time.realtimeSinceStartup;
            var lookup = locator.Find("missing", 0.02f).GetAwaiter();
            while (!lookup.IsCompleted) yield return null;
            Assert.AreEqual(UiElementLookupStatus.RendererUnavailable, lookup.GetResult().Status);
            Assert.Less(Time.realtimeSinceStartup - start, 1f);
            locator.StopSession();
        }

        [UnityTest]
        public IEnumerator OuterWaitBudgetPreservesLastStatusAndStopsAtDeadline()
        {
            using var fixture = new PlaytestConnectionFixture();
            LogAssert.Expect(LogType.Warning, new Regex("Web UI gave no lookup answer"));
            var start = Time.realtimeSinceStartup;
            var operation = PlaytestUiElementOps.WaitUiElement("missing", 0.02f).GetAwaiter();
            while (!operation.IsCompleted) yield return null;
            var error = Assert.Throws<TimeoutException>(() => operation.GetResult());
            StringAssert.Contains("RendererUnavailable", error.Message);
            Assert.Less(Time.realtimeSinceStartup - start, 1f);
        }

        [UnityTest]
        public IEnumerator TextBudgetStopsBeforeAnyRetryAfterDeadline()
        {
            using var fixture = new PlaytestConnectionFixture();
            LogAssert.Expect(LogType.Warning, new Regex("Web UI gave no text-input answer"));
            var start = Time.realtimeSinceStartup;
            var operation = PlaytestUiElementOps.SetText("field", "text", 0.02f).GetAwaiter();
            while (!operation.IsCompleted) yield return null;
            var error = Assert.Throws<TimeoutException>(() => operation.GetResult());
            StringAssert.Contains("RendererUnavailable", error.Message);
            Assert.Less(Time.realtimeSinceStartup - start, 1f);
            Assert.IsNotNull(fixture.Read(fixture.Cef));
            Assert.IsNull(fixture.Read(fixture.Cef));
        }

        [UnityTest]
        public IEnumerator StopStartTerminatesOldTextRetryWithoutSecondDelivery()
        {
            using var fixture = new PlaytestConnectionFixture();
            var operation = PlaytestUiElementOps.SetText("field", "old", 15f).GetAwaiter();
            Assert.IsNotNull(fixture.Read(fixture.Cef));
            PlaytestUiLocatorSession.Stop();
            PlaytestUiLocatorSession.Start();
            LogAssert.Expect(LogType.Warning, new Regex("Web UI gave no text-input answer"));
            while (!operation.IsCompleted) yield return null;
            var error = Assert.Throws<InvalidOperationException>(() => operation.GetResult());
            StringAssert.Contains("stopped session", error.Message);
            Assert.IsNull(fixture.Read(fixture.Cef));
            Assert.IsNull(fixture.Read(fixture.External));
        }

        [UnityTest]
        public IEnumerator InvalidResponseEndsLocatorWithContractReason()
        {
            using var fixture = new PlaytestConnectionFixture();
            var locator = new WebUiElementLocator();
            locator.StartSession();
            var operation = locator.SetText("field", "value", 1f).GetAwaiter();
            var id = JObject.Parse(fixture.Read(fixture.Cef))["data"]["requestId"].Value<string>();
            var payload = new JObject { ["requestId"] = id, ["status"] = "typed" };
            LogAssert.Expect(LogType.Warning, new Regex("malformed playtest.dom_set_text_result"));
            fixture.Hub.ConnectionActions.Resolve("playtest.dom_set_text_result").ExecuteAsync(fixture.CefId, payload).GetAwaiter().GetResult();
            while (!operation.IsCompleted) yield return null;
            var error = Assert.Throws<InvalidOperationException>(() => operation.GetResult());
            StringAssert.Contains("Malformed", error.Message);
            locator.StopSession();
        }

        [Test]
        public void OtherConnectionCannotPoisonPendingWithMalformedResponse()
        {
            using var fixture = new PlaytestConnectionFixture();
            var channel = new PlaytestWebUiRequestChannel<DomSetTextResult>();
            var sender = fixture.CefId;
            var id = channel.Open(sender, fixture.Hub);
            var handler = new DomSetTextResultActionHandler(channel);
            var payload = new JObject { ["requestId"] = id, ["status"] = "typed" };
            LogAssert.Expect(LogType.Warning, new Regex("unrelated connection"));
            Assert.AreEqual("unrelated_response", handler.ExecuteAsync(Guid.NewGuid(), payload).GetAwaiter().GetResult().Error);
            LogAssert.Expect(LogType.Warning, new Regex("malformed playtest.dom_set_text_result"));
            Assert.AreEqual("invalid_payload", handler.ExecuteAsync(sender, payload).GetAwaiter().GetResult().Error);
            var response = channel.Await(id, 1f).GetAwaiter().GetResult();
            Assert.AreEqual(WebUiResponseState.Rejected, response.State);
            StringAssert.Contains("Malformed", response.Reason);
        }

        [Test]
        public void DuplicateSnapshotIsAmbiguous()
        {
            var result = new DomQueryResult { MatchCount = 2 };
            Assert.AreEqual(UiElementLookupStatus.Ambiguous, WebUiElementLocator.ToSnapshot("duplicate", result, Rect.zero).Status);
        }
    }
}
