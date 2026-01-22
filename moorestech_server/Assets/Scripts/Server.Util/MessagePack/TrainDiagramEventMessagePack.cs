using System;
using Game.Train.SaveLoad;
using MessagePack;

namespace Server.Util.MessagePack
{
    public enum TrainDiagramEventType
    {
        Docked = 0,
        Departed = 1,
    }

    [MessagePackObject]
    public class TrainDiagramEventMessagePack
    {
        [Key(0)] public TrainDiagramEventType EventType { get; set; }
        [Key(1)] public Guid TrainId { get; set; }
        [Key(2)] public Guid EntryId { get; set; }
        [Key(3)] public ConnectionDestinationMessagePack Node { get; set; }
        [Key(4)] public long Tick { get; set; }
        [Key(5)] public uint DiagramHash { get; set; }

        [Obsolete("Reserved for MessagePack.")]
        public TrainDiagramEventMessagePack()
        {
        }

        public TrainDiagramEventMessagePack(TrainDiagramEventType eventType, Guid trainId, Guid entryId, ConnectionDestination node, long tick, uint diagramHash)
        {
            EventType = eventType;
            TrainId = trainId;
            EntryId = entryId;
            Node = new ConnectionDestinationMessagePack(node);
            Tick = tick;
            DiagramHash = diagramHash;
        }
    }
}
