using System;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.RailGraph;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UniRx;
using VContainer.Unity;
using Client.Game.TickSynchronization;
using Client.Game.InGame.Train.Network.TickSynchronization;

namespace Client.Game.InGame.Train.Network
{
    /// <summary>
    ///     RailConnection差分イベントを受け取りpost-sim tickでキャッシュへ反映する
    ///     Receives rail connection diffs and applies them on post-simulation tick
    /// </summary>
    public sealed class RailGraphConnectionNetworkHandler : IInitializable, IDisposable
    {
        private readonly TrainTickContext _context;
        private readonly RailGraphClientCache _cache;
        private readonly CompositeDisposable _subscriptions = new();

        public RailGraphClientCache Cache => _cache;

        public RailGraphConnectionNetworkHandler(TrainTickContext context, RailGraphClientCache cache)
        {
            _context = context;
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
            
        #region Internal
            void OnConnectionCreated(byte[] payload)
            {
                // 生成差分を復号し、post-simキューへ積む
                // Deserialize creation diff and enqueue into post-sim buffer
                var message = MessagePackSerializer.Deserialize<RailConnectionCreatedMessagePack>(payload);
                if (message == null)
                {
                    return;
                }
                _context.Events.EnqueueEvent(message.ServerTick, message.TickSequenceId, CreateBufferedEvent(message));
                
                
                ITickBufferedEvent CreateBufferedEvent(RailConnectionCreatedMessagePack messagePack)
                {
                    return TickBufferedEvent.Create(ApplyCreatedConnection);
                    
                    void ApplyCreatedConnection()
                    {
                        // 適用時点でGuid整合性を再確認してから接続を反映する
                        // Recheck guid consistency at apply time before upserting connection
                        if (!_cache.TryValidateEndpoint(messagePack.FromNodeId, messagePack.FromGuid))
                        {
                            return;
                        }
                        if (!_cache.TryValidateEndpoint(messagePack.ToNodeId, messagePack.ToGuid))
                        {
                            return;
                        }
                        _cache.UpsertConnection(
                            messagePack.FromNodeId,
                            messagePack.ToNodeId,
                            messagePack.Distance,
                            messagePack.RailTypeGuid,
                            messagePack.IsDrawable);
                    }
                }
            }
            #endregion
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
        
        private void OnConnectionRemoved(byte[] payload)
        {
            // 削除差分を復号し、post-simキューへ積む
            // Deserialize removal diff and enqueue into post-sim buffer
            var message = MessagePackSerializer.Deserialize<RailConnectionRemovedMessagePack>(payload);
            if (message == null)
            {
                return;
            }
            _context.Events.EnqueueEvent(message.ServerTick, message.TickSequenceId, CreateBufferedEvent(message));

        #region Internal
            ITickBufferedEvent CreateBufferedEvent(RailConnectionRemovedMessagePack messagePack)
            {
                return TickBufferedEvent.Create(ApplyRemovedConnection);

                void ApplyRemovedConnection()
                {
                    // 適用時点で両端のGuidが一致した場合のみ削除する
                    // Remove connection only when both endpoint guids still match
                    if (!_cache.TryValidateEndpoint(messagePack.FromNodeId, messagePack.FromGuid))
                    {
                        return;
                    }
                    if (!_cache.TryValidateEndpoint(messagePack.ToNodeId, messagePack.ToGuid))
                    {
                        return;
                    }
                    _cache.RemoveConnection(messagePack.FromNodeId, messagePack.ToNodeId);
                }
            }
        #endregion
        }
    }
}
