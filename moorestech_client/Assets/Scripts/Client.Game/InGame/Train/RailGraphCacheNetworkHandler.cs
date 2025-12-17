using Client.Game.InGame.Context;
using Game.Train.RailGraph;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using System;
using UniRx;
using UnityEngine;
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
        private readonly CompositeDisposable _subscriptions = new();

        public RailGraphCacheNetworkHandler(RailGraphClientCache cache)
        {
            _cache = cache;
        }

        public void Initialize()
        {
            // RailNode生成イベントを購読して差分適用を待機
            // Subscribe to node creation events for diff application
            ClientContext.VanillaApi.Event.SubscribeEventResponse(
                RailNodeCreatedEventPacket.EventTag,
                OnRailNodeCreated).AddTo(_subscriptions);
            // RailNode削除イベントも購読し整合性保持
            // Also subscribe to removal events for consistency
            ClientContext.VanillaApi.Event.SubscribeEventResponse(
                RailNodeRemovedEventPacket.EventTag,
                OnRailNodeRemoved).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        #region Internal

        private void OnRailNodeCreated(byte[] payload)
        {
            // メッセージを復号しキャッシュへUpsert
            // Decode message and upsert into cache
            var message = MessagePackSerializer.Deserialize<RailNodeCreatedMessagePack>(payload);
            var destination = message.ConnectionDestination?.ToConnectionDestination() ?? ConnectionDestination.Default;

            _cache.UpsertNode(
                message.NodeId,
                message.NodeGuid,
                message.OriginPoint.ToUnityVector(),
                destination,
                message.FrontControlPoint.ToUnityVector(),
                message.BackControlPoint.ToUnityVector(),
                message.Tick);
        }

        private void OnRailNodeRemoved(byte[] payload)
        {
            // 削除メッセージを解析してGuid一致を確認
            // Decode removal payload and verify guid consistency
            var message = MessagePackSerializer.Deserialize<RailNodeRemovedMessagePack>(payload);
            if (_cache.TryValidateEndpoint(message.NodeId, message.NodeGuid))
            // Guidが一致した場合のみノード削除
            // Remove the node only when guid matches
            _cache.RemoveNode(message.NodeId, message.Tick);
        }

        #endregion
    }
}
