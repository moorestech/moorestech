using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Context;
using Game.Train.Unit;
using MessagePack;
using Server.Util.MessagePack.BeltTransport;
using UniRx;
namespace Server.Event.EventReceive
{
    public sealed class BeltTickCompletedEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:beltTickCompleted";
        private readonly EventProtocolProvider _events;
        private readonly BeltWorldTransport _transport;
        private readonly TrainUpdateService _trainUpdateService;
        public BeltTickCompletedEventPacket(EventProtocolProvider events, BeltWorldTransport transport, TrainUpdateService trainUpdateService)
        { _events = events; _transport = transport; _trainUpdateService = trainUpdateService; }
        public void Load()
        {
            _transport.OnTickCompleted.Subscribe(Publish);
            #region Internal
            void Publish(BeltTickDifference difference)
            {
                // 全段階と配置変更が確定した1tickを一つの通知にする。
                // Publish all completed stages and topology mutations in a single tick bundle.
                var payload = MessagePackSerializer.Serialize(new BeltTickMessagePack(difference,
                    _trainUpdateService.GetCurrentTick(), _trainUpdateService.NextTickSequenceId()));
                _events.AddBroadcastEvent(EventTag, payload);
            }
            #endregion
        }

    }
}
