using System;
using System.Collections.Concurrent;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Boot.Transport
{
    // 接続の実IDを必要とする応答だけの受け口
    // Receive actions that require the actual connection identity
    public interface IConnectionActionHandler
    {
        string ActionType { get; }
        UniTask<ActionResult> ExecuteAsync(Guid connectionId, JObject payload);
    }

    public sealed class ConnectionActionRegistry
    {
        private readonly ConcurrentDictionary<string, IConnectionActionHandler> _handlers = new();

        private readonly WebSocketHub _hub;

        internal ConnectionActionRegistry(WebSocketHub hub) => _hub = hub;

        public void Register(IConnectionActionHandler handler)
        {
            if (_hub.ResolveAction(handler.ActionType) != null)
                throw new InvalidOperationException($"Action already registered without connection context: {handler.ActionType}");
            _handlers[handler.ActionType] = handler;
        }

        public IConnectionActionHandler Resolve(string actionType)
        {
            return _handlers.TryGetValue(actionType, out var handler) ? handler : null;
        }

        internal void Clear() => _handlers.Clear();
    }
}
