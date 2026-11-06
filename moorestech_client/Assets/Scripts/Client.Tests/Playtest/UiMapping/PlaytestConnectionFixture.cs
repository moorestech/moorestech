using System;
using System.Collections.Concurrent;
using System.Reflection;
using Client.Playtest.UiElements;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Cef;

namespace Client.Tests.Playtest.UiMapping
{
    internal sealed class PlaytestConnectionFixture : IDisposable
    {
        private const BindingFlags Fields = BindingFlags.NonPublic | BindingFlags.Instance;
        private readonly FieldInfo _hostField = typeof(global::Client.WebUiHost.Boot.WebUiHost).GetField("_hub", BindingFlags.NonPublic | BindingFlags.Static);
        private readonly object _previousHub;
        public readonly WebSocketHub Hub = new();
        public readonly Guid CefId = Guid.NewGuid();
        public readonly Guid ExternalId = Guid.NewGuid();
        public readonly WebSocketConnection Cef;
        public readonly WebSocketConnection External;

        public PlaytestConnectionFixture()
        {
            _previousHub = _hostField.GetValue(null);
            _hostField.SetValue(null, Hub);
            Cef = Connect(CefId);
            External = Connect(ExternalId);
            var navigation = WebUiCefConnection.BeginNavigation(Hub);
            if (!WebUiCefConnection.TryRegister(Hub, navigation, CefId)) throw new InvalidOperationException("Fixture CEF registration failed");
            PlaytestUiLocatorSession.Start();
        }

        public string Read(WebSocketConnection connection)
        {
            var channel = typeof(WebSocketConnection).GetField("_sendChannel", Fields).GetValue(connection);
            var reader = channel.GetType().GetProperty("Reader").GetValue(channel);
            var arguments = new object[] { null };
            return (bool)reader.GetType().GetMethod("TryRead").Invoke(reader, arguments) ? (string)arguments[0] : null;
        }

        public WebSocketConnection Connect(Guid id)
        {
            var connection = new WebSocketConnection(null, id);
            connection.Topics.TryAdd("playtest.dom_query", 0);
            connection.Topics.TryAdd("playtest.dom_set_text", 0);
            var connections = (ConcurrentDictionary<Guid, WebSocketConnection>)typeof(WebSocketHub).GetField("_connections", Fields).GetValue(Hub);
            connections.TryAdd(id, connection);
            return connection;
        }

        public void Dispose()
        {
            Cef.RequestStop();
            External.RequestStop();
            PlaytestUiLocatorSession.Stop();
            WebUiCefConnection.Release(Hub, CefId);
            _hostField.SetValue(null, _previousHub);
        }
    }
}
