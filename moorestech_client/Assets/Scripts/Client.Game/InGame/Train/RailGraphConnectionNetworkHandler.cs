using System;
using Client.Game.InGame.Context;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UniRx;
using VContainer.Unity;

namespace Client.Game.InGame.Train
{
    /// <summary>
    ///     RailConnection差分イベントを受け取りクライアントキャッシュへ反映する
    /// </summary>
    public sealed class RailGraphConnectionNetworkHandler : IInitializable, IDisposable
    {
        private readonly RailGraphClientCache _cache;
        private readonly CompositeDisposable _subscriptions = new();

        public static RailGraphConnectionNetworkHandler Instance { get; private set; }
        public RailGraphClientCache Cache => _cache;

        public RailGraphConnectionNetworkHandler(RailGraphClientCache cache)
        {
            _cache = cache;
            Instance = this;
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
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #region Internal

        private void OnConnectionCreated(byte[] payload)
        {
            // 生成イベントを復号してGuid整合性を確認
            // Decode creation payload and validate guid consistency
            var message = MessagePackSerializer.Deserialize<RailConnectionCreatedMessagePack>(payload);
            if (!TryValidateEndpoint(message.FromNodeId, message.FromGuid))
            {
                return;
            }

            if (!TryValidateEndpoint(message.ToNodeId, message.ToGuid))
            {
                return;
            }

            // 問題なければキャッシュに接続を反映
            // Apply the connection diff to the cache
            _cache.UpsertConnection(message.FromNodeId, message.ToNodeId, message.Distance, 0);
        }

        private void OnConnectionRemoved(byte[] payload)
        {
            // 削除イベントを復号して現在のGuidと照合
            // Decode removal payload and check current guid
            var message = MessagePackSerializer.Deserialize<RailConnectionRemovedMessagePack>(payload);
            if (!TryValidateEndpoint(message.FromNodeId, message.FromGuid))
            {
                return;
            }

            if (!TryValidateEndpoint(message.ToNodeId, message.ToGuid))
            {
                return;
            }

            // 両端一致時のみ接続情報を削除
            // Remove the cached connection only when both ends match
            _cache.RemoveConnection(message.FromNodeId, message.ToNodeId, 0);
        }

        private bool TryValidateEndpoint(int nodeId, Guid guid)
        {
            // キャッシュ済みノードかつGuid一致でtrue
            // Return true only when the node exists and guid matches
            if (!_cache.TryGetNode(nodeId, out var cachedGuid, out _))
            {
                return false;
            }

            return cachedGuid == guid;
        }

        #endregion
    }
}
