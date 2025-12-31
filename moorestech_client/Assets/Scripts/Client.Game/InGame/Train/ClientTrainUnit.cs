using Game.Train.RailGraph;
using Game.Train.Train;
using Server.Util.MessagePack;
using System;

namespace Client.Game.InGame.Train
{
    // クライアント上で扱う最小限の列車データ
    // Minimal client-side representation of a train
    public sealed class ClientTrainUnit
    {
        public ClientTrainUnit(Guid trainId)
        {
            TrainId = trainId;
        }

        public Guid TrainId { get; }
        public TrainSimulationSnapshot Simulation { get; private set; }
        public TrainDiagramSnapshot Diagram { get; private set; }
        public RailPosition RailPosition { get; private set; }
        public long LastUpdatedTick { get; private set; }
        public TrainDiagramEventMessagePack LastDiagramEvent { get; private set; }

        // スナップショットの内容で内部状態を更新
        // Update internal state by the received snapshot
        public void Update(TrainSimulationSnapshot simulation, TrainDiagramSnapshot diagram, RailPositionSaveData railPosition, long tick)
        {
            Simulation = simulation;
            Diagram = diagram;
            RailPosition = RailPositionFactory.Restore(railPosition);
            LastUpdatedTick = tick;
        }

        public void ApplyDiagramEvent(TrainDiagramEventMessagePack message)
        {
            if (message == null || message.TrainId != TrainId)
            {
                return;
            }

            LastDiagramEvent = message;
            if (message.EventType == TrainDiagramEventType.Departed)
            {
                UpdateDiagramIndexByEntryId(message.EntryId);
            }
        }

        private void UpdateDiagramIndexByEntryId(Guid entryId)
        {
            if (Diagram.Entries == null || Diagram.Entries.Count == 0)
            {
                return;
            }

            var entries = Diagram.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].EntryId == entryId)
                {
                    Diagram = new TrainDiagramSnapshot(i, entries);
                    return;
                }
            }
        }
    }
}
