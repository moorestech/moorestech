using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Client.Input;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.WebUiHost.Transport
{
    // 休止前に送られメインスレッド待ちだった ui_state.request が、休止中に実行されず web_ui_suspended で返ることを固定する（D-1）
    // Pin that a ui_state.request sent before suspension and awaiting the main thread is answered web_ui_suspended instead of running (D-1)
    public sealed class WebSocketSuspendedUiStateRequestTest
    {
        [TearDown]
        public void TearDown()
        {
            UiInputExclusivity.SetWebUiSuspended(false);
        }

        [UnityTest]
        public IEnumerator 休止中のui_state_requestは実行されずweb_ui_suspendedで返る()
        {
            var handler = new CountingUiStateActionHandler();
            var result = RunAction(handler, RequestUiStateActionHandler.Type, true, out var waiting);
            while (waiting.Status == UniTaskStatus.Pending) yield return null;

            var parsed = JObject.Parse(result());
            Assert.AreEqual(0, handler.ExecutedCount, "休止中に画面遷移要求が実行された");
            Assert.IsFalse(parsed["ok"].Value<bool>());
            Assert.AreEqual("web_ui_suspended", parsed["error"].Value<string>());
        }

        [UnityTest]
        public IEnumerator 休止していなければui_state_requestは実行される()
        {
            var handler = new CountingUiStateActionHandler();
            var result = RunAction(handler, RequestUiStateActionHandler.Type, false, out var waiting);
            while (waiting.Status == UniTaskStatus.Pending) yield return null;

            Assert.AreEqual(1, handler.ExecutedCount);
            Assert.IsTrue(JObject.Parse(result())["ok"].Value<bool>());
        }

        // スレッドプールから渡し、メインスレッド待ちに入ってから休止を切り替える（休止前に届いた要求の再現）
        // Hand it over from the thread pool and toggle suspension once it awaits the main thread (reproduces a request that arrived before suspension)
        private static Func<string> RunAction(IActionHandler handler, string type, bool suspend, out UniTask waiting)
        {
            var hub = new WebSocketHub();
            hub.RegisterAction(handler);
            var connection = new WebSocketConnection(null, Guid.NewGuid());
            var dispatcher = new WebSocketMessageDispatcher(hub);
            var message = new WsClientMessage { Op = "action", Type = type, RequestId = "action-1" };

            waiting = Task.Run(() => dispatcher.HandleActionAsync(connection, message)).Result;
            if (suspend)
            {
                LogAssert.Expect(LogType.Warning, new Regex("web_ui_suspended"));
                UiInputExclusivity.SetWebUiSuspended(true);
            }
            return () =>
            {
                var sent = ReadSent(connection);
                connection.RequestStop();
                return sent;
            };
        }

        // 送信ループを持たない接続なので、送信キューから直接1件取り出す
        // The connection has no send loop, so take one entry straight from its send queue
        private static string ReadSent(WebSocketConnection connection)
        {
            var channel = typeof(WebSocketConnection).GetField("_sendChannel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(connection);
            var reader = channel.GetType().GetProperty("Reader").GetValue(channel);
            var arguments = new object[] { null };
            return (bool)reader.GetType().GetMethod("TryRead").Invoke(reader, arguments) ? (string)arguments[0] : null;
        }

        private sealed class CountingUiStateActionHandler : IActionHandler
        {
            public int ExecutedCount { get; private set; }
            public string ActionType => RequestUiStateActionHandler.Type;

            public UniTask<ActionResult> ExecuteAsync(JObject payload)
            {
                ExecutedCount++;
                return UniTask.FromResult(ActionResult.Success());
            }
        }
    }
}
