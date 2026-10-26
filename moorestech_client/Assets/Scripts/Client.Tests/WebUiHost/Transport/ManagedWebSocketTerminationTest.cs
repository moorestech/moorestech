using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Text.RegularExpressions;
using Client.WebUiHost.Boot;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.WebUiHost.Transport
{
    public class ManagedWebSocketTerminationTest
    {
        [UnityTest]
        public IEnumerator ActualManagedSocketsTerminateAfterBrokenAndHealthyNetworkOutput()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var brokenPeer = new TcpClient();
            using var healthyPeer = new TcpClient();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            brokenPeer.Connect(IPAddress.Loopback, port);
            using var brokenServer = listener.AcceptTcpClient();
            healthyPeer.Connect(IPAddress.Loopback, port);
            using var healthyServer = listener.AcceptTcpClient();
            listener.Stop();

            // 実ManagedWebSocketとloopbackの送信ストリームで境界を検証する
            // Exercise actual ManagedWebSocket network output over loopback streams
            var brokenStream = brokenServer.GetStream();
            using var broken = WebSocket.CreateFromStream(brokenStream, true, null, TimeSpan.Zero);
            using var healthy = WebSocket.CreateFromStream(healthyServer.GetStream(), true, null, TimeSpan.Zero);
            brokenStream.Dispose();
            var hub = new WebSocketHub();
            var brokenId = Guid.NewGuid();
            var healthyId = Guid.NewGuid();
            var connections = (ConcurrentDictionary<Guid, WebSocketConnection>)typeof(WebSocketHub)
                .GetField("_connections", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hub);
            connections.TryAdd(brokenId, new WebSocketConnection(broken, brokenId));
            connections.TryAdd(healthyId, new WebSocketConnection(healthy, healthyId));
            LogAssert.Expect(LogType.Error, new Regex("close failed"));
            var closing = hub.CloseAllAsync();
            while (!closing.IsCompleted) yield return null;
            var result = closing.GetAwaiter().GetResult();
            Assert.IsTrue(result.Succeeded);
            Assert.IsTrue(result.CloseErrors.ContainsKey(brokenId));
            Assert.IsFalse(result.CloseErrors.ContainsKey(healthyId));
            Assert.That(broken.State, Is.EqualTo(WebSocketState.Aborted).Or.EqualTo(WebSocketState.Closed));
            Assert.That(healthy.State, Is.EqualTo(WebSocketState.Aborted).Or.EqualTo(WebSocketState.Closed));
            Debug.Log($"Actual socket final states: broken={broken.State}, healthy={healthy.State}");
        }
    }
}
