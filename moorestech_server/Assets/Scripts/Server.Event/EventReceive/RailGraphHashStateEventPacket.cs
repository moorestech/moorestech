using System;
using Game.Train.Unit;
using Game.Train.RailGraph;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    /// <summary>
    ///     RailGraphのハッシュとTickを定期的に配信するイベントサービス
    ///     Periodically broadcasts the current RailGraph hash/tick pair to clients
    /// </summary>
    public sealed class RailGraphHashStateEventPacket : IDisposable
    {
        public const string EventTag = "va:event:railGraphHashState";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly TrainUpdateService _trainUpdateService;
        private readonly CompositeDisposable _disposables = new();

        public RailGraphHashStateEventPacket(EventProtocolProvider eventProtocolProvider, IRailGraphDatastore railGraphDatastore, TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _railGraphDatastore = railGraphDatastore;
            _trainUpdateService = trainUpdateService;

            // Trainのtick通知に合わせてRailGraphハッシュを送信
            // Broadcast RailGraph hash in sync with train tick notifications.
            _trainUpdateService.OnHashEvent
                .Subscribe(BroadcastHashState)
                .AddTo(_disposables);
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }

        #region Internal

        private void BroadcastHashState(long tick)
        {
            // RailGraphのハッシュとtickを取得し全プレイヤーに送信
            // Fetch the latest graph hash/tick and broadcast to every player
            var hash = _railGraphDatastore.GetConnectNodesHash();
            var payload = MessagePackSerializer.Serialize(new RailGraphHashStateMessagePack(hash, tick));
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }

        #endregion
    }
}

