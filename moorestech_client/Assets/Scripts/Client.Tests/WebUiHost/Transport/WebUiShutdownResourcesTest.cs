using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Client.Game.Common;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Boot.Transport;
using Client.WebUiHost.Vite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.WebUiHost.Transport
{
    public class WebUiShutdownResourcesTest
    {
        [UnityTest]
        public IEnumerator ProcessFailureDoesNotSkipListenerAndBothResourcesAreReported()
        {
            using var process = new Process();
            var vite = BrokenVite(process);
            var kestrel = new KestrelServer();
            var host = new FailingWebHost();
            SetField(kestrel, "_webHost", host);
            ExpectFailures();
            var stopping = WebUiHostShutdown.StopAsync(vite, null, kestrel).GetAwaiter();
            while (!stopping.IsCompleted) yield return null;
            var result = stopping.GetResult();
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(1, host.StopAttempts);
            Assert.AreEqual(2, result.ResourceFailures.Count);
            Assert.IsTrue(result.ResourceFailures.ContainsKey("Vite process"));
            StringAssert.Contains("injected listener shutdown failure", result.ResourceFailures["Kestrel listener"]);
            Assert.IsEmpty(result.Connections.UnterminatedConnections);
        }

        [UnityTest]
        public IEnumerator ListenerFailureReachesShutdownPipeline()
        {
            var kestrel = new KestrelServer();
            SetField(kestrel, "_webHost", new FailingWebHost());
            var field = typeof(global::Client.WebUiHost.Boot.WebUiHost).GetField("_kestrel", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNull(field.GetValue(null));
            field.SetValue(null, kestrel);
            LogAssert.Expect(LogType.Error, new Regex("Kestrel listener stop failed"));
            LogAssert.Expect(LogType.Error, new Regex("shutdown incomplete"));
            GameShutdownEvent.ResetForNewSession();
            GameShutdownEvent.RegisterParticipant(new WebUiHostShutdownParticipant());
            var stopping = GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason.IntentionalExit).GetAwaiter();
            while (!stopping.IsCompleted) yield return null;
            Assert.AreEqual(ShutdownFlushResult.FlushFailed, stopping.GetResult());
            GameShutdownEvent.ResetForNewSession();
        }

        [Test]
        public void StartReregistersSameParticipantAfterSessionReset()
        {
            var host = typeof(global::Client.WebUiHost.Boot.WebUiHost);
            var kestrelField = host.GetField("_kestrel", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNull(kestrelField.GetValue(null));
            var participants = (IList)typeof(GameShutdownEvent)
                .GetField("_participants", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            kestrelField.SetValue(null, new KestrelServer());
            try
            {
                // 起動済みHost経路でもReset後に同じ参加者を登録し直す
                // Re-register the same participant after reset even through the already-running host path
                GameShutdownEvent.ResetForNewSession();
                Assert.IsTrue(global::Client.WebUiHost.Boot.WebUiHost.StartAsync(CancellationToken.None).GetAwaiter().GetResult());
                Assert.AreEqual(1, participants.Count);
                var original = participants[0];
                GameShutdownEvent.ResetForNewSession();
                Assert.AreEqual(0, participants.Count);
                Assert.IsTrue(global::Client.WebUiHost.Boot.WebUiHost.StartAsync(CancellationToken.None).GetAwaiter().GetResult());
                Assert.IsTrue(global::Client.WebUiHost.Boot.WebUiHost.StartAsync(CancellationToken.None).GetAwaiter().GetResult());
                Assert.AreEqual(1, participants.Count);
                Assert.AreSame(original, participants[0]);
            }
            finally
            {
                kestrelField.SetValue(null, null);
                GameShutdownEvent.ResetForNewSession();
            }
        }

        private static ViteSupervisor BrokenVite(Process process)
        {
            // 未関連付けの実ProcessでOSプロセス照会の失敗を注入する
            // Inject an OS process-query failure with an actual unassociated Process
            var child = new ViteProcess();
            SetField(child, "_process", process);
            var vite = new ViteSupervisor();
            SetField(vite, "_process", child);
            return vite;
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        }

        private static void ExpectFailures()
        {
            LogAssert.Expect(LogType.Error, new Regex("Vite process stop failed"));
            LogAssert.Expect(LogType.Error, new Regex("Kestrel listener stop failed"));
            LogAssert.Expect(LogType.Error, new Regex("shutdown incomplete"));
        }

        private sealed class FailingWebHost : IWebHost
        {
            public int StopAttempts { get; private set; }
            public IFeatureCollection ServerFeatures => throw new NotSupportedException();
            public IServiceProvider Services => throw new NotSupportedException();
            public void Dispose() { }
            public void Start() => throw new NotSupportedException();
            public Task StartAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task StopAsync(CancellationToken cancellationToken)
            {
                StopAttempts++;
                return Task.FromException(new InvalidOperationException("injected listener shutdown failure"));
            }
        }
    }
}
