using Game.Train.Unit;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    // TrainUnitのpre sim差分を通知するイベントパケット
    // Event packet that broadcasts pre-simulation TrainUnit diffs.
    public sealed class TrainUnitPreSimulationDiffEventPacket
    {
        public const string EventTag = "va:event:trainUnitPreSimulationDiff";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly TrainUpdateService _trainUpdateService;

        public TrainUnitPreSimulationDiffEventPacket(EventProtocolProvider eventProtocolProvider, TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _trainUpdateService = trainUpdateService;
            _trainUpdateService.GetOnPreSimulationDiffEvent().Subscribe(OnPreSimulationDiff);
        }

        #region Internal

        private void OnPreSimulationDiff(TrainUpdateService.TrainTickDiffBatch batch)
        {
            if (batch.Diffs == null || batch.Diffs.Count == 0)
            {
                return;
            }

            var payload = MessagePackSerializer.Serialize(new TrainUnitPreSimulationDiffMessagePack(batch));
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }

        #endregion
    }
}
