using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
            _files = new RemoteExecTestFiles();
        }

        [TearDown]
        public void TearDown()
        {
            RemoteExecAccessFile.ClearToken();
            _files.Restore();
        }

        [Test]
        public async Task コンパイル段の例外をログと失敗応答と台帳へ残す()
        {
            // 実HTTP待受なしで入口を通し、コンパイル段の例外を再現する
            // Exercise the endpoint without a listener and simulate a compile-stage exception
            LogAssert.Expect(LogType.Warning, new Regex("入口を開きました"));
            RemoteExecAccessFile.Issue(0);
            var context = new DefaultHttpContext();
            context.Request.Method = "POST";
            context.Request.Headers.AppendCommaSeparatedValues(RemoteExecAccessFile.HeaderName, new[] { RemoteExecAccessFile.Token });
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"code\":\"return 1 + 1;\",\"target\":\"client\"}"));
            context.Response.Body = new MemoryStream();

            LogAssert.Expect(LogType.Error, new Regex("compile-stage failure"));
            await RemoteExecEndpoint.HandleAsync(context, new ThrowingRunner());

            context.Response.Body.Position = 0;
            var response = JObject.Parse(new StreamReader(context.Response.Body).ReadToEnd());
            Assert.AreEqual(200, context.Response.StatusCode);
            Assert.IsFalse(response.Value<bool>("ok"));
            StringAssert.Contains("compile-stage failure", response.Value<string>("exception"));
            var entries = _files.ReadLedgerEntries();
            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual("start", entries[0].Value<string>("event"));
            Assert.AreEqual("result", entries[1].Value<string>("event"));
            Assert.IsFalse(entries[1].Value<bool>("ok"));
            Assert.AreEqual(entries[0].Value<long>("sequence"), entries[1].Value<long>("sequence"));
        }

        private sealed class ThrowingRunner : IRemoteExecHttpRunner
        {
            public UniTask<RemoteExecResult> RunAsync(string body, RemoteExecTarget target)
            {
                throw new InvalidOperationException("compile-stage failure");
            }
        }
    }
}
