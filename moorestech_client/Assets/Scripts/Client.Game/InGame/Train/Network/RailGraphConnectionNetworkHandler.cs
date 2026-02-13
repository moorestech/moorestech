using System;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.RailGraph;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UniRx;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Network
{
    /// <summary>
    ///     RailConnection差分イベントを受け取りクライアントキャッシュへ反映する
    /// </summary>
    public sealed class RailGraphConnectionNetworkHandler : IInitializable, IDisposable
    {
        private readonly RailGraphClientCache _cache;
        private readonly CompositeDisposable _subscriptions = new();

        public RailGraphClientCache Cache => _cache;

        public RailGraphConnectionNetworkHandler(RailGraphClientCache cache)
        {
            _cache = cache;
        }

        public void Initialize()
        {
            ClientContext.VanillaApi.Event.SubscribeEventResponse(
                RailConnectionCreatedEventPacket.EventTag,
                OnConnectionCreated).AddTo(_subscriptions);
            ClientContext.VanillaApi.Event.SubscribeEventResponse(
                RailConnectionRemovedEventPacket.EventTag,
                OnConnectionRemoved).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        #region Internal

        private void OnConnectionCreated(byte[] payload)
        {
            // 生成イベントを復号してGuid整合性を確認
            // Decode creation payload and validate guid consistency
            var message = MessagePackSerializer.Deserialize<RailConnectionCreatedMessagePack>(payload);
            if ((message == null) || (_cache == null))
                return;
            if (!_cache.TryValidateEndpoint(message.FromNodeId, message.FromGuid))
                return;
            if (!_cache.TryValidateEndpoint(message.ToNodeId, message.ToGuid))
                return;
            // 問題なければキャッシュに接続を反映
            // Apply the connection diff to the cache
            _cache.UpsertConnection(message.FromNodeId, message.ToNodeId, message.Distance, message.RailTypeGuid, message.IsDrawable);
        }

        private void OnConnectionRemoved(byte[] payload)
        {
            // 削除イベントを復号して現在のGuidと照合
            // Decode removal payload and check current guid
            var message = MessagePackSerializer.Deserialize<RailConnectionRemovedMessagePack>(payload);
            if ((message == null) || (_cache == null))
                return;
            if (!_cache.TryValidateEndpoint(message.FromNodeId, message.FromGuid))
                return;
            if (!_cache.TryValidateEndpoint(message.ToNodeId, message.ToGuid))
                return;
            // 両端一致時のみ接続情報を削除
            // Remove the cached connection only when both ends match
            _cache.RemoveConnection(message.FromNodeId, message.ToNodeId);
        }

        #endregion
    }
}
