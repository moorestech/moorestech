using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using Client.Game.Common;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Boot.Transport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.WebUiHost.Transport
{
    public class WebSocketTerminationFailureTest
    {
        [UnityTest]
        public IEnumerator FailedAbortStillDisposesAndPropagatesUnterminatedIdentity()
        {
            var hub = new WebSocketHub();
            var id = Guid.NewGuid();
            var socket = new CloseFailureSocket(true, true, true);
            Add(hub, socket, id);
            ExpectFailures(false);
            var closing = hub.CloseAllAsync();
            while (!closing.IsCompleted) yield return null;
            var result = closing.GetAwaiter().GetResult();
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(1, socket.DisposeAttempts);
            StringAssert.Contains("abort failed", result.CloseErrors[id]);
            StringAssert.Contains("dispose failed", result.CloseErrors[id]);
            StringAssert.Contains("Open", result.UnterminatedConnections[id]);
        }

        [UnityTest]
        public IEnumerator ShutdownPipelineReturnsFailureForUnterminatedSocket()
        {
            var field = typeof(global::Client.WebUiHost.Boot.WebUiHost).GetField("_hub", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNull(field.GetValue(null));
            var hub = new WebSocketHub();
            Add(hub, new CloseFailureSocket(true, true, true), Guid.NewGuid());
            field.SetValue(null, hub);
            ExpectFailures(true);
            GameShutdownEvent.ResetForNewSession();
            GameShutdownEvent.RegisterParticipant(new WebUiHostShutdownParticipant());
            var stopping = GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason.IntentionalExit).GetAwaiter();
            while (!stopping.IsCompleted) yield return null;
            Assert.AreEqual(ShutdownFlushResult.FlushFailed, stopping.GetResult());
            GameShutdownEvent.ResetForNewSession();
        }

        [UnityTest]
        public IEnumerator DisposeCanRecoverAfterAbortFailureWithoutLosingErrors()
        {
            var hub = new WebSocketHub();
            var id = Guid.NewGuid();
            var socket = new CloseFailureSocket(true, true, false);
            Add(hub, socket, id);
            LogAssert.Expect(LogType.Error, new Regex("close failed"));
            LogAssert.Expect(LogType.Error, new Regex("abort failed"));
            var closing = hub.CloseAllAsync();
            while (!closing.IsCompleted) yield return null;
            var result = closing.GetAwaiter().GetResult();
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(System.Net.WebSockets.WebSocketState.Closed, socket.State);
            StringAssert.Contains("abort failed", result.CloseErrors[id]);
            Assert.IsEmpty(result.UnterminatedConnections);
        }

        private static void Add(WebSocketHub hub, CloseFailureSocket socket, Guid id)
        {
            var connections = (ConcurrentDictionary<Guid, WebSocketConnection>)typeof(WebSocketHub)
                .GetField("_connections", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hub);
            connections.TryAdd(id, new WebSocketConnection(socket, id));
        }

        private static void ExpectFailures(bool host)
        {
            LogAssert.Expect(LogType.Error, new Regex("close failed"));
            LogAssert.Expect(LogType.Error, new Regex("abort failed"));
            LogAssert.Expect(LogType.Error, new Regex("dispose failed"));
            LogAssert.Expect(LogType.Error, new Regex("unterminated"));
            if (host) LogAssert.Expect(LogType.Error, new Regex("shutdown incomplete"));
        }
    }
}
