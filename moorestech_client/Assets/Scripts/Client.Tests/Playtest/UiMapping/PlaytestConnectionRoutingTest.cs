using System;
using System.Collections;
using System.Text.RegularExpressions;
using Client.Playtest.UiElements;
using Cysharp.Threading.Tasks;
using Client.Playtest.WebUi;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Cef;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Playtest.UiMapping
{
    public class PlaytestConnectionRoutingTest
    {
        [UnityTest]
        public IEnumerator DirectedTextDeliveryAndDispatcherUseActualConnection()
        {
            using var fixture = new PlaytestConnectionFixture();
            var request = new PlaytestDomSetText();
            fixture.Hub.ConnectionActions.Register(request.ResultHandler);
            var pending = request.Request(fixture.Hub, "field", "requested", 1f).GetAwaiter();
            var envelope = JObject.Parse(fixture.Read(fixture.Cef));
            Assert.IsNull(fixture.Read(fixture.External));
            var id = envelope["data"]["requestId"].Value<string>();
            var message = new WsClientMessage
            {
                Op = "action", Type = "playtest.dom_set_text_result", RequestId = "action-1",
                Payload = new JObject { ["requestId"] = id, ["status"] = "applied", ["matchCount"] = 1, ["actualValue"] = "requested" },
            };
            // 同じpayloadでも実接続が違えば保留を解決できない
            // Identical payloads cannot resolve a request from a different actual connection
            var dispatcher = new WebSocketMessageDispatcher(fixture.Hub);
            LogAssert.Expect(LogType.Warning, new Regex("unrelated connection"));
            var externalAction = dispatcher.HandleActionAsync(fixture.External, message).GetAwaiter();
            while (!externalAction.IsCompleted) yield return null;
            externalAction.GetResult();
            Assert.AreEqual("unrelated_response", JObject.Parse(fixture.Read(fixture.External))["error"].Value<string>());
            Assert.IsFalse(pending.IsCompleted);
            var cefAction = dispatcher.HandleActionAsync(fixture.Cef, message).GetAwaiter();
            while (!cefAction.IsCompleted || !pending.IsCompleted) yield return null;
            cefAction.GetResult();
            Assert.AreEqual(UiSetTextStatus.Applied, pending.GetResult().Result.Status);
            Assert.IsTrue(JObject.Parse(fixture.Read(fixture.Cef))["ok"].Value<bool>());
        }

        [Test]
        public void DateLikeTextIsDeliveredVerbatim()
        {
            // 日時の形をした入力文字列も、送信時に日時として解釈し直さず原文のまま届ける
            // Date-shaped input text reaches CEF verbatim instead of being reparsed as a date
            using var fixture = new PlaytestConnectionFixture();
            const string dateLikeText = "2026-10-28T12:00:00.000+09:00";
            new PlaytestDomSetText().Request(fixture.Hub, "field", dateLikeText, 1f).Forget();
            StringAssert.Contains(dateLikeText, fixture.Read(fixture.Cef));
        }

        [Test]
        public void ExternalConnectionAloneIsUnavailable()
        {
            using var fixture = new PlaytestConnectionFixture();
            WebUiCefConnection.Release(fixture.Hub, fixture.CefId);
            LogAssert.Expect(LogType.Warning, "[PlaytestUi] No registered CEF connection");
            var response = new PlaytestDomSetText().Request(fixture.Hub, "field", "text", 1f).GetAwaiter().GetResult();
            Assert.IsFalse(response.HasResponse);
            Assert.IsNull(fixture.Read(fixture.External));
            Assert.IsNull(fixture.Read(fixture.Cef));
        }

        [Test]
        public void ReconnectedPageCannotAnswerOldConnectionRequest()
        {
            using var fixture = new PlaytestConnectionFixture();
            var channel = new PlaytestWebUiRequestChannel<DomSetTextResult>();
            var id = channel.Open(fixture.CefId, fixture.Hub);
            var nextId = Guid.NewGuid();
            fixture.Connect(nextId);
            var navigation = WebUiCefConnection.BeginNavigation(fixture.Hub);
            Assert.IsTrue(WebUiCefConnection.TryRegister(fixture.Hub, navigation, nextId));
            WebUiCefConnection.Release(fixture.Hub, fixture.CefId);
            Assert.IsTrue(WebUiCefConnection.TryGetConnection(fixture.Hub, out var active));
            Assert.AreEqual(nextId, active);
            LogAssert.Expect(LogType.Warning, new Regex("unrelated connection"));
            Assert.IsFalse(channel.Accepts(id, nextId));
            LogAssert.Expect(LogType.Warning, new Regex("unrelated connection"));
            Assert.IsFalse(channel.Accepts(id, fixture.CefId));
            Assert.AreEqual(WebUiResponseState.Stopped, channel.Await(id, 1f).GetAwaiter().GetResult().State);
        }
    }
}
