using Game.Train.Diagram;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    public sealed class TrainDiagramEventPacket
    {
        public const string DockedEventTag = "va:event:trainDiagramDocked";
        public const string DepartedEventTag = "va:event:trainDiagramDeparted";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly CompositeDisposable _disposable = new();

        public TrainDiagramEventPacket(EventProtocolProvider eventProtocolProvider, TrainDiagramManager diagramManager)
        {
            _eventProtocolProvider = eventProtocolProvider;

            diagramManager.TrainDocked
                .Subscribe(data => BroadcastEvent(data, TrainDiagramEventType.Docked))
                .AddTo(_disposable);
            diagramManager.TrainDeparted
                .Subscribe(data => BroadcastEvent(data, TrainDiagramEventType.Departed))
                .AddTo(_disposable);
        }

        private void BroadcastEvent(TrainDiagramManager.TrainDiagramEventData data, TrainDiagramEventType eventType)
        {
            if (data.Entry == null || data.Entry.Node == null)
            {
                return;
            }

            var message = new TrainDiagramEventMessagePack(
                eventType,
                data.Context.TrainId,
                data.Entry.entryId,
                data.Entry.Node.ConnectionDestination,
                data.Tick,
                data.DiagramHash);
            var payload = MessagePackSerializer.Serialize(message);
            var tag = eventType == TrainDiagramEventType.Docked ? DockedEventTag : DepartedEventTag;
            _eventProtocolProvider.AddBroadcastEvent(tag, payload);
        }
    }
}

