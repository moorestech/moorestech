using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Text.RegularExpressions;
using Client.Game.Common;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Boot.Transport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Host = Client.WebUiHost.Boot.WebUiHost;

namespace Client.Tests.WebUiHost.Transport
{
    public class WebUiStopGenerationTest
    {
        private WebUiHostTestContext _lifetime;
        [SetUp] public void BeginLifetime() => _lifetime = new WebUiHostTestContext();
        [TearDown] public void EndLifetime() => _lifetime.Dispose();

        [UnityTest]
        public IEnumerator CompletedFailureSurvivesRepeatedStopAndShutdownParticipant()
        {
            PrepareFailingHost();
            var stopping = Host.StopAsync().GetAwaiter();
            while (!stopping.IsCompleted) yield return null;
            var first = stopping.GetResult();
            Assert.IsFalse(first.Succeeded);
            var repeated = Host.StopAsync().GetAwaiter();
            while (!repeated.IsCompleted) yield return null;
            Assert.AreSame(first, repeated.GetResult(), "Same lifetime must retain the completed failure object");
            var participant = new WebUiHostShutdownParticipant().FlushOnShutdownAsync().GetAwaiter();
            while (!participant.IsCompleted) yield return null;
            Assert.AreEqual(ShutdownFlushResult.FlushFailed, participant.GetResult());
            LogAssert.Expect(LogType.Error, new Regex("editor cleanup completed with shutdown failures"));
            Host.StopAndWaitSync(TimeSpan.FromSeconds(4));
            var final = Host.StopAsync().GetAwaiter();
            while (!final.IsCompleted) yield return null;
            Assert.AreSame(first, final.GetResult());
        }
        [UnityTest]
        public IEnumerator GenuineStartCreatesNewStopGeneration()
        {
            PrepareFailingHost();
            var first = Host.StopAsync().GetAwaiter();
            while (!first.IsCompleted) yield return null;
            var prior = first.GetResult();
            Assert.IsFalse(prior.Succeeded);
            var repeated = Host.StopAsync().GetAwaiter();
            while (!repeated.IsCompleted) yield return null;
            Assert.AreSame(prior, repeated.GetResult());
            try
            {
                // 実Kestrel/Viteを起動し、偽のフィールド代入なしで次世代を通す
                // Start actual Kestrel/Vite and enter the next lifetime without faking host fields
                var starting = Host.StartAsync(CancellationToken.None).GetAwaiter();
                while (!starting.IsCompleted) yield return null;
                Assert.IsTrue(starting.GetResult());
                var stopping = Host.StopAsync().GetAwaiter();
                while (!stopping.IsCompleted) yield return null;
                var next = stopping.GetResult();
                Assert.IsTrue(next.Succeeded);
                Assert.AreNotSame(prior, next);
                var repeatedNext = Host.StopAsync().GetAwaiter();
                while (!repeatedNext.IsCompleted) yield return null;
                Assert.AreSame(next, repeatedNext.GetResult());
            }
            finally
            {
                Host.StopAndWaitSync(TimeSpan.FromSeconds(4));
                GameShutdownEvent.ResetForNewSession();
            }
        }

        private static void PrepareFailingHost()
        {
            var hub = new WebSocketHub();
            var id = Guid.NewGuid();
            var socket = new CloseFailureSocket(true, true, true);
            var connections = (ConcurrentDictionary<Guid, WebSocketConnection>)typeof(WebSocketHub)
                .GetField("_connections", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hub);
            connections.TryAdd(id, new WebSocketConnection(socket, id));
            typeof(Host).GetField("_hub", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, hub);
            foreach (var pattern in new[] { "close failed", "abort failed", "dispose failed", "unterminated", "shutdown incomplete" })
                LogAssert.Expect(LogType.Error, new Regex(pattern));
        }
    }
}
