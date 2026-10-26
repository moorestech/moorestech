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
        private WebUiHostTestContext _lifetime;
        [SetUp] public void BeginLifetime() => _lifetime = new WebUiHostTestContext();
        [TearDown] public void EndLifetime() => _lifetime.Dispose();

        [UnityTest]
        public IEnumerator FailedCloseReportsIdentityAndStillClosesOtherConnections()
        {
            var hub = new WebSocketHub();
            var failedId = Guid.NewGuid();
            var failed = new CloseFailureSocket(true, false, false);
            var healthy = new CloseFailureSocket(false, false, false);
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
            Assert.That(failed.State, Is.EqualTo(System.Net.WebSockets.WebSocketState.Closed).Or.EqualTo(System.Net.WebSockets.WebSocketState.Aborted));
            Assert.That(healthy.State, Is.EqualTo(System.Net.WebSockets.WebSocketState.Closed).Or.EqualTo(System.Net.WebSockets.WebSocketState.Aborted));
            Assert.IsFalse(hub.HasConnections);
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(1, result.CloseErrors.Count);
            StringAssert.Contains("injected close output failure", result.CloseErrors[failedId]);
        }
        [UnityTest]
        public IEnumerator HostStopRetainsRecoveredCloseErrorAfterReleasingItsHub()
        {
            var field = typeof(global::Client.WebUiHost.Boot.WebUiHost).GetField("_hub", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNull(field.GetValue(null), "Test requires an inactive host");
            var hub = new WebSocketHub();
            var id = Guid.NewGuid();
            var socket = new CloseFailureSocket(true, false, false);
            var connections = (ConcurrentDictionary<Guid, WebSocketConnection>)typeof(WebSocketHub)
                .GetField("_connections", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hub);
            connections.TryAdd(id, new WebSocketConnection(socket, id));
            field.SetValue(null, hub);
            LogAssert.Expect(LogType.Error, new Regex("close failed"));
            var stopping = global::Client.WebUiHost.Boot.WebUiHost.StopAsync().GetAwaiter();
            while (!stopping.IsCompleted) yield return null;
            var result = stopping.GetResult();
            Assert.IsTrue(result.Succeeded);
            StringAssert.Contains("injected close output failure", result.Connections.CloseErrors[id]);
            Assert.IsNull(global::Client.WebUiHost.Boot.WebUiHost.Hub);
            Assert.IsFalse(hub.HasConnections);
        }

    }
}
