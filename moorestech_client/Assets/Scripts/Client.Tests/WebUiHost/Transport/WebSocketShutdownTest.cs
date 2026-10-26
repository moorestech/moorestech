using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using Client.WebUiHost.Boot;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.WebUiHost.Transport
{
    public class WebSocketShutdownTest
    {
        [UnityTest]
        public IEnumerator FailedCloseReportsIdentityAndStillClosesOtherConnections()
        {
            var hub = new WebSocketHub();
            var failedId = Guid.NewGuid();
            var failed = new CloseFailureSocket(true);
            var healthy = new CloseFailureSocket(false);
            var connections = (ConcurrentDictionary<Guid, WebSocketConnection>)typeof(WebSocketHub)
                .GetField("_connections", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hub);
            connections.TryAdd(failedId, new WebSocketConnection(failed, failedId));
            var healthyId = Guid.NewGuid();
            connections.TryAdd(healthyId, new WebSocketConnection(healthy, healthyId));
            LogAssert.Expect(LogType.Error, new Regex("close failed"));
            var closing = hub.CloseAllAsync();
            while (!closing.IsCompleted) yield return null;
            var result = closing.GetAwaiter().GetResult();
            Assert.AreEqual(1, failed.CloseAttempts);
            Assert.AreEqual(1, healthy.CloseAttempts);
            Assert.IsFalse(hub.HasConnections);
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(1, result.FailedConnections.Count);
            StringAssert.Contains("injected close output failure", result.FailedConnections[failedId]);
        }
        [UnityTest]
        public IEnumerator HostStopReturnsFailureAfterReleasingItsHub()
        {
            var field = typeof(global::Client.WebUiHost.Boot.WebUiHost).GetField("_hub", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNull(field.GetValue(null), "Test requires an inactive host");
            var hub = new WebSocketHub();
            var id = Guid.NewGuid();
            var socket = new CloseFailureSocket(true);
            var connections = (ConcurrentDictionary<Guid, WebSocketConnection>)typeof(WebSocketHub)
                .GetField("_connections", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hub);
            connections.TryAdd(id, new WebSocketConnection(socket, id));
            field.SetValue(null, hub);
            LogAssert.Expect(LogType.Error, new Regex("close failed"));
            LogAssert.Expect(LogType.Error, new Regex("stopped with failed WebSocket closes"));
            var stopping = global::Client.WebUiHost.Boot.WebUiHost.StopAsync().GetAwaiter();
            while (!stopping.IsCompleted) yield return null;
            var result = stopping.GetResult();
            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("injected close output failure", result.FailedConnections[id]);
            Assert.IsNull(global::Client.WebUiHost.Boot.WebUiHost.Hub);
            Assert.IsFalse(hub.HasConnections);
        }

    }
}
