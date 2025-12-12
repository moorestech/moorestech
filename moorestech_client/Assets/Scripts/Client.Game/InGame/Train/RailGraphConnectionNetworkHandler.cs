using System;
using Client.Game.InGame.Context;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using VContainer.Unity;

namespace Client.Game.InGame.Train
{
    /// <summary>
    ///     RailConnection差分イベントを受け取りクライアントキャッシュへ反映する
    /// </summary>
    public sealed class RailGraphConnectionNetworkHandler : IInitializable, IDisposable
    {
        private readonly RailGraphClientCache _cache;
        private IDisposable _subscription;

        public event Action<RailConnectionCreatedMessagePack> ConnectionCreated;
        public static RailGraphConnectionNetworkHandler Instance { get; private set; }

        public RailGraphConnectionNetworkHandler(RailGraphClientCache cache)
        {
            _cache = cache;
            Instance = this;
        }

        public void Initialize()
        {
            _subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(
                RailConnectionCreatedEventPacket.EventTag,
                OnConnectionCreated);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnConnectionCreated(byte[] payload)
        {
            var message = MessagePackSerializer.Deserialize<RailConnectionCreatedMessagePack>(payload);
            if (!TryValidateEndpoint(message.FromNodeId, message.FromGuid)) return;
            if (!TryValidateEndpoint(message.ToNodeId, message.ToGuid)) return;

            _cache.UpsertConnection(message.FromNodeId, message.ToNodeId, message.Distance, 0);
            ConnectionCreated?.Invoke(message);

            bool TryValidateEndpoint(int nodeId, Guid guid)
            {
                if (!_cache.TryGetNode(nodeId, out var cachedGuid, out _))
                {
                    return false;
                }
                return cachedGuid == guid;
            }
        }
    }
}
