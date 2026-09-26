using System;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.RailGraph;
using Game.Train.SaveLoad;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UniRx;
using VContainer.Unity;
using Client.Game.Common.TickSynchronization;
using Client.Game.InGame.Train.Network.TickSynchronization;

namespace Client.Game.InGame.Train.Network
{
    /// <summary>
    ///     RailNode差分イベントを購読してpost-sim tickでキャッシュへ反映する
    ///     Subscribes to rail node diffs and applies them on post-simulation tick
    /// </summary>
    public sealed class RailGraphCacheNetworkHandler : IInitializable, IDisposable
    {
        private readonly TrainTickContext _context;
        private readonly RailGraphClientCache _cache;
        private readonly ClientStationReferenceRegistry _stationReferenceRegistry;
        private readonly CompositeDisposable _subscriptions = new();

        public RailGraphCacheNetworkHandler(
            TrainTickContext context,
            RailGraphClientCache cache,
            ClientStationReferenceRegistry stationReferenceRegistry)
        {
            _context = context;
            _cache = cache;
            _stationReferenceRegistry = stationReferenceRegistry;
        }

        public void Initialize()
        {
            // RailNode生成イベントを購読してtick同期反映を待機する
            // Subscribe node-creation diffs and wait for tick-aligned apply
            ClientContext.VanillaApi.Event.SubscribeEventResponse(
                RailNodeCreatedEventPacket.EventTag,
                OnRailNodeCreated).AddTo(_subscriptions);
            // RailNode削除イベントも同じtick同期経路へ流す
            // Route node-removal diffs through the same tick-aligned path
            ClientContext.VanillaApi.Event.SubscribeEventResponse(
                RailNodeRemovedEventPacket.EventTag,
                OnRailNodeRemoved).AddTo(_subscriptions);
            
        #region Internal

            void OnRailNodeCreated(byte[] payload)
            {
                // 受信差分を復号し、post-simキューへ積む
                // Deserialize incoming diff and enqueue into post-sim buffer
                var message = MessagePackSerializer.Deserialize<RailNodeCreatedMessagePack>(payload);
                if (message == null)
                {
                    return;
                }
                _context.Events.EnqueueEvent(message.ServerTick, message.TickSequenceId, CreateBufferedEvent(message));
                return;

                ITickBufferedEvent CreateBufferedEvent(RailNodeCreatedMessagePack messagePack)
                {
                    return TickBufferedEvent.Create(ApplyCreatedNode);

                    void ApplyCreatedNode()
                    {
                        // ノード追加と駅参照更新を同一tickで反映する
                        // Apply node insertion and station reference update on the same tick
                        var destination = messagePack.ConnectionDestination?.ToConnectionDestination() ?? ConnectionDestination.Default;
                        _cache.UpsertNode(
                            messagePack.NodeId,
                            messagePack.NodeGuid,
                            messagePack.OriginPoint.ToUnityVector(),
                            destination,
                            messagePack.FrontControlPoint.ToUnityVector(),
                            messagePack.BackControlPoint.ToUnityVector());
                        _stationReferenceRegistry.ApplyStationReference(destination);
                    }
                }
            }

            void OnRailNodeRemoved(byte[] payload)
            {
                // 削除差分を復号し、post-simキューへ積む
                // Deserialize removal diff and enqueue into post-sim buffer
                var message = MessagePackSerializer.Deserialize<RailNodeRemovedMessagePack>(payload);
                if (message == null)
                {
                    return;
                }
                _context.Events.EnqueueEvent(message.ServerTick, message.TickSequenceId, CreateBufferedEvent(message));

                ITickBufferedEvent CreateBufferedEvent(RailNodeRemovedMessagePack messagePack)
                {
                    return TickBufferedEvent.Create(ApplyRemovedNode);

                    void ApplyRemovedNode()
                    {
                        // Guid一致時のみノード削除を適用する
                        // Remove node only when guid still matches at apply time
                        if (_cache.TryValidateEndpoint(messagePack.NodeId, messagePack.NodeGuid))
                        {
                            _cache.RemoveNode(messagePack.NodeId);
                        }
                    }
                }
            }

        #endregion
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

    }
}
