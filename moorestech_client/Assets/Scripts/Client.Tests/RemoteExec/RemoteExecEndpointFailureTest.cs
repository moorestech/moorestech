using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Client.RemoteExec;
using Client.RemoteExec.Access;
using Client.RemoteExec.Run;
using Cysharp.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.RemoteExec
{
    public sealed class RemoteExecEndpointFailureTest
    {
        private RemoteExecTestFiles _files;

        [SetUp]
        public void SetUp()
        {
            RemoteExecLedger.Initialize("session_" + DateTime.UtcNow.Ticks, null);
            _files = new RemoteExecTestFiles();
            RemoteExecAccessFile.ClearToken();
        }

        [TearDown]
        public void TearDown()
        {
            RemoteExecLaunchOption.ResolveFromCommandLine(Array.Empty<string>());
            RemoteExecAccessFile.ClearToken();
            _files.Restore();
        }

        [Test]
        public void WebUi起動失敗ではポート値に関係なく接続情報を発行しない()
        {
            LogAssert.Expect(LogType.Warning, new Regex("--remoteExec が指定された"));
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { "--remoteExec" });

            LogAssert.Expect(LogType.Error, new Regex("Web UI サーバーが起動していない"));
            RemoteExecActivation.ActivateIfRequested(false, 12345);

            Assert.IsNull(RemoteExecAccessFile.Token);
        }

        [Test]
        public async Task 無効起動では要求を404で隠して理由をログに残す()
        {
            RemoteExecLaunchOption.ResolveFromCommandLine(Array.Empty<string>());
            var context = new DefaultHttpContext();
            context.Request.Method = "POST";
            context.Response.Body = new MemoryStream();

            LogAssert.Expect(LogType.Warning, new Regex("起動オプションが無いため要求を404で拒否"));
            await RemoteExecEndpoint.HandleAsync(context);

            Assert.AreEqual(404, context.Response.StatusCode);
            Assert.AreEqual(0, _files.ReadLedgerEntries().Length);
        }

        [Test]
        public async Task 実行処理の想定外例外をログと失敗応答と台帳へ残す()
        {
            // 実HTTP待受なしで入口を通し、実行処理の例外を再現する
            // Exercise the endpoint without a listener and simulate an execution failure
            LogAssert.Expect(LogType.Warning, new Regex("--remoteExec が指定された"));
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { "--remoteExec" });
            LogAssert.Expect(LogType.Warning, new Regex("入口を開きました"));
            RemoteExecAccessFile.Issue(0);
            var context = new DefaultHttpContext();
            context.Request.Method = "POST";
            context.Request.Headers.AppendCommaSeparatedValues(RemoteExecAccessFile.HeaderName, new[] { RemoteExecAccessFile.Token });
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"code\":\"return 1 + 1;\",\"target\":\"client\"}"));
            context.Response.Body = new MemoryStream();

            LogAssert.Expect(LogType.Error, new Regex("execution failure"));
            await RemoteExecEndpoint.HandleAsync(context, new ThrowingRunner());

            context.Response.Body.Position = 0;
            var response = JObject.Parse(new StreamReader(context.Response.Body).ReadToEnd());
            Assert.AreEqual(200, context.Response.StatusCode);
            Assert.AreEqual("RuntimeException", response.Value<string>("outcome"));
            StringAssert.Contains("execution failure", response.Value<string>("exception"));
            var entries = _files.ReadLedgerEntries();
            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual("start", entries[0].Value<string>("event"));
            Assert.AreEqual("result", entries[1].Value<string>("event"));
            Assert.AreEqual("RuntimeException", entries[1].Value<string>("outcome"));
            Assert.AreEqual(entries[0].Value<long>("sequence"), entries[1].Value<long>("sequence"));
        }

        private sealed class ThrowingRunner : IRemoteExecHttpRunner
        {
            public UniTask<RemoteExecResult> RunAsync(string body, RemoteExecTarget target, CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("execution failure");
            }
        }
    }
}
