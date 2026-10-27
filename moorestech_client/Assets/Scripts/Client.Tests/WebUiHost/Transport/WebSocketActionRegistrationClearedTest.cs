using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.WebUiHost.Transport
{
    // 停止していないのにメインスレッド待ちの間に登録が外れた action は、停止中ではなく未知の action として返すことを固定する
    // Pin that an action whose registration was cleared without stopping, while it awaited the main thread, is answered as unknown rather than stopping
    public sealed class WebSocketActionRegistrationClearedTest
    {
        [UnityTest]
        public IEnumerator 停止せずに登録が外れたactionはunknown_actionで返す()
        {
            var hub = new WebSocketHub();
            hub.RegisterAction(new NoopActionHandler());
            var connection = new WebSocketConnection(null, Guid.NewGuid());
            var dispatcher = new WebSocketMessageDispatcher(hub);
            var message = new WsClientMessage { Op = "action", Type = NoopActionHandler.Type, RequestId = "action-1" };

            // スレッドプールから渡し、登録の確認を済ませてメインスレッド待ちに入ったところで戻す
            // Hand it over from the thread pool and come back once it passed the registration check and awaits the main thread
            var dispatching = Task.Run(() => dispatcher.HandleActionAsync(connection, message)).Result;
            LogAssert.Expect(LogType.Warning, new Regex("unknown_action"));
            hub.ClearBindings();
            while (dispatching.Status == UniTaskStatus.Pending) yield return null;
            dispatching.GetAwaiter().GetResult();

            var result = JObject.Parse(ReadSent(connection));
            connection.RequestStop();
            Assert.IsFalse(result["ok"].Value<bool>());
            Assert.AreEqual("unknown_action", result["error"].Value<string>(), "停止していないのに host_stopping と名乗った");
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

        private sealed class NoopActionHandler : IActionHandler
        {
            public const string Type = "test.noop";
            public string ActionType => Type;
            public UniTask<ActionResult> ExecuteAsync(JObject payload) => UniTask.FromResult(ActionResult.Success());
        }
    }
}
