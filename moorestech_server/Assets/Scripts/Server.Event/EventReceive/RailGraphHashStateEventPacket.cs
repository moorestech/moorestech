using System;
using Game.Train.Common;
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
        private readonly CompositeDisposable _disposables = new();

        public RailGraphHashStateEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;

            // 1秒周期でRailGraphハッシュを通知
            // Broadcast hash/tick every second
            Observable.Interval(TimeSpan.FromSeconds(TrainUpdateService.HashBroadcastIntervalSeconds))
                .Subscribe(_ => BroadcastHashState())
                .AddTo(_disposables);
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }

        #region Internal

        private void BroadcastHashState()
        {
            // RailGraphのハッシュとtickを取得し全プレイヤーに送信
            // Fetch the latest graph hash/tick and broadcast to every player
            var hash = RailGraphDatastore.GetConnectNodesHash();
            var payload = MessagePackSerializer.Serialize(new RailGraphHashStateMessagePack(hash, TrainUpdateService.CurrentTick));
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }

        #endregion
    }
}
