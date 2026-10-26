using System.Globalization;
using System.Text.RegularExpressions;
using Client.Playtest.UiElements;
using Client.Playtest.WebUi;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Playtest
{
    public class PlaytestWebUiLocatorContractTest
    {
        [Test]
        public void DomQueryResultWithFlagsResolvesPendingRequest()
        {
            var channel = new PlaytestWebUiRequestChannel<DomQueryResult>();
            var handler = new DomQueryResultActionHandler(channel);
            var requestId = channel.Open();

            var actionResult = handler.ExecuteAsync(QueryPayload(requestId, true, 1, new JArray("selected"))).GetAwaiter().GetResult();
            var response = channel.Await(requestId, 1f).GetAwaiter().GetResult();

            Assert.IsTrue(actionResult.Ok);
            Assert.IsTrue(response.HasResponse);
            Assert.AreEqual(1, response.Result.MatchCount);
            CollectionAssert.AreEqual(new[] { "selected" }, response.Result.StateFlags);
        }

        [Test]
        public void DomQueryResultWithoutStateFlagsIsRejected()
        {
            var channel = new PlaytestWebUiRequestChannel<DomQueryResult>();
            var payload = QueryPayload(channel.Open(), true, 1, new JArray());
            payload.Remove("stateFlags");

            LogAssert.Expect(LogType.Warning, "[PlaytestUi] Rejected malformed playtest.dom_query_result payload");
            var actionResult = new DomQueryResultActionHandler(channel).ExecuteAsync(payload).GetAwaiter().GetResult();

            Assert.IsFalse(actionResult.Ok);
            Assert.AreEqual("invalid_payload", actionResult.Error);
        }

        [Test]
        public void FoundWithZeroMatchesIsRejected()
        {
            var channel = new PlaytestWebUiRequestChannel<DomQueryResult>();
            LogAssert.Expect(LogType.Warning, "[PlaytestUi] Rejected inconsistent playtest.dom_query_result payload");
            var actionResult = new DomQueryResultActionHandler(channel).ExecuteAsync(QueryPayload(channel.Open(), true, 0, new JArray())).GetAwaiter().GetResult();
            Assert.IsFalse(actionResult.Ok);
        }

        [Test]
        public void SnapshotCarriesFieldsAndWarnsOnDuplicates()
        {
            var result = new DomQueryResult { Found = true, Visible = true, HitTestPassed = true, Text = "送信", MatchCount = 2, StateFlags = new[] { "selected" } };
            LogAssert.Expect(LogType.Warning, new Regex("matched 2 Web elements"));

            var snapshot = WebUiElementLocator.ToSnapshot("bug-report-send", result, new Rect(10f, 20f, 30f, 40f));

            Assert.AreEqual(UiElementLookupStatus.Found, snapshot.Status);
            Assert.IsTrue(snapshot.IsFound && snapshot.Visible && snapshot.Clickable);
            Assert.AreEqual(new Rect(10f, 20f, 30f, 40f), snapshot.ScreenRect);
            Assert.AreEqual("送信", snapshot.Text);
            Assert.IsTrue(snapshot.HasStateFlag("selected"));
            Assert.IsFalse(snapshot.HasStateFlag("ghost"));
        }

        [Test]
        public void NotFoundSnapshotIsEmpty()
        {
            var snapshot = UiElementSnapshot.NotFound("missing", UiRenderer.Web);
            Assert.IsFalse(snapshot.IsFound || snapshot.Visible || snapshot.Clickable);
            Assert.AreEqual(Rect.zero, snapshot.ScreenRect);
            Assert.AreEqual(string.Empty, snapshot.Text);
            Assert.IsFalse(snapshot.HasStateFlag("selected"));
        }

        [TestCase("applied", UiSetTextStatus.Applied)]
        [TestCase("notInteractable", UiSetTextStatus.NotInteractable)]
        [TestCase("valueMismatch", UiSetTextStatus.ValueMismatch)]
        public void SetTextResultMapsStatusWord(string word, UiSetTextStatus expected)
        {
            var channel = new PlaytestWebUiRequestChannel<DomSetTextResult>();
            var requestId = channel.Open();
            var payload = new JObject { ["requestId"] = requestId, ["status"] = word, ["matchCount"] = 1, ["actualValue"] = "abc" };

            var actionResult = new DomSetTextResultActionHandler(channel).ExecuteAsync(payload).GetAwaiter().GetResult();
            var response = channel.Await(requestId, 1f).GetAwaiter().GetResult();

            Assert.IsTrue(actionResult.Ok);
            Assert.AreEqual(expected, response.Result.Status);
            Assert.AreEqual("abc", response.Result.ActualValue);
        }

        [Test]
        public void AwaitStopsWaitingOnceReset()
        {
            var channel = new PlaytestWebUiRequestChannel<DomQueryResult>();
            var requestId = channel.Open();
            channel.Reset();

            // Resetで保留が消えたら、期限を待たずに応答なしで返る
            // Once Reset drops the entry, Await returns no-response without waiting out the deadline
            var awaiter = channel.Await(requestId, 10f).GetAwaiter();
            Assert.IsTrue(awaiter.IsCompleted);
            Assert.IsFalse(awaiter.GetResult().HasResponse);
        }

        [Test]
        public void UnknownSetTextStatusIsRejected()
        {
            var channel = new PlaytestWebUiRequestChannel<DomSetTextResult>();
            var payload = new JObject { ["requestId"] = channel.Open(), ["status"] = "typed", ["matchCount"] = 1, ["actualValue"] = "" };
            LogAssert.Expect(LogType.Warning, "[PlaytestUi] Rejected malformed playtest.dom_set_text_result payload");
            var actionResult = new DomSetTextResultActionHandler(channel).ExecuteAsync(payload).GetAwaiter().GetResult();
            Assert.IsFalse(actionResult.Ok);
        }

        [Test]
        public void OversizedMatchCountIsRejectedWithoutConversionException()
        {
            var channel = new PlaytestWebUiRequestChannel<DomQueryResult>();
            var payload = QueryPayload(channel.Open(), true, 1, new JArray());
            payload["matchCount"] = long.MaxValue;
            LogAssert.Expect(LogType.Warning, "[PlaytestUi] Rejected malformed playtest.dom_query_result payload");

            var result = new DomQueryResultActionHandler(channel).ExecuteAsync(payload).GetAwaiter().GetResult();

            Assert.IsFalse(result.Ok);
        }

        [TestCase("en-US")]
        [TestCase("ja-JP")]
        [TestCase("fr-FR")]
        public void FloatingPayloadIsIndependentOfCurrentCulture(string cultureName)
        {
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                var payload = new JObject { ["x"] = new JValue(1.5) };

                Assert.IsTrue(WebUiPayloadReader.TryReadFloat(payload, "x", out var value));
                Assert.AreEqual(1.5f, value);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [Test]
        public void AppliedWithZeroMatchesIsRejected()
        {
            var channel = new PlaytestWebUiRequestChannel<DomSetTextResult>();
            var payload = new JObject { ["requestId"] = channel.Open(), ["status"] = "applied", ["matchCount"] = 0, ["actualValue"] = "abc" };
            LogAssert.Expect(LogType.Warning, "[PlaytestUi] Rejected inconsistent playtest.dom_set_text_result match count");

            var result = new DomSetTextResultActionHandler(channel).ExecuteAsync(payload).GetAwaiter().GetResult();

            Assert.IsFalse(result.Ok);
        }

        private static JObject QueryPayload(string requestId, bool found, int matchCount, JArray stateFlags)
        {
            return new JObject
            {
                ["requestId"] = requestId, ["found"] = found, ["visible"] = found,
                ["x"] = 1, ["y"] = 2, ["width"] = 10, ["height"] = 5, ["devicePixelRatio"] = 1,
                ["hitTestPassed"] = found, ["text"] = "t", ["matchCount"] = matchCount, ["stateFlags"] = stateFlags,
            };
        }
    }
}
