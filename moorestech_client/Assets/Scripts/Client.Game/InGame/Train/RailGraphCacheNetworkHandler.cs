using System;
using Client.Game.InGame.Context;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using VContainer.Unity;

namespace Client.Game.InGame.Train
{
    /// <summary>
    ///     RailNode生成イベントを購読してクライアントキャッシュへ反映する
    ///     Subscribes to rail node creation events and updates the local cache
    /// </summary>
    public sealed class RailGraphCacheNetworkHandler : IInitializable, IDisposable
    {
        private readonly RailGraphClientCache _cache;
        private IDisposable _subscription;

        public RailGraphCacheNetworkHandler(RailGraphClientCache cache)
        {
            _cache = cache;
        }

        public void Initialize()
        {
            // RailNode生成イベントを購読して差分適用を待機
            // Subscribe to node creation events for diff application
            _subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(
                RailNodeCreatedEventPacket.EventTag,
                OnRailNodeCreated);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        #region Internal

        private void OnRailNodeCreated(byte[] payload)
        {
            // メッセージを復号しキャッシュへUpsert
            // Decode message and upsert into cache
            var message = MessagePackSerializer.Deserialize<RailNodeCreatedMessagePack>(payload);
            var destination = message.ConnectionDestination?.ToClientDestination() ?? ConnectionDestination.Default;

            _cache.UpsertNode(
                message.NodeId,
                message.NodeGuid,
                message.ControlPointOrigin.ToUnityVector(),
                destination,
                message.PrimaryControlPointPosition.ToUnityVector(),
                message.OppositeControlPointPosition.ToUnityVector(),
                0);
        }

        #endregion
    }
}
