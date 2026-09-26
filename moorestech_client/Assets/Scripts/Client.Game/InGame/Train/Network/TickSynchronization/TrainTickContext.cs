using Client.Game.TickSynchronization;

namespace Client.Game.InGame.Train.Network.TickSynchronization
{
    // train/rail stream専用の同期状態を所有する。
    // Own synchronization state exclusively for the train/rail stream.
    public sealed class TrainTickContext
    {
        internal bool IsInitialSnapshotApplied { get; private set; }
        internal readonly ClientTickState State;
        internal readonly TickEventBuffer Events;
        internal readonly ClientTickAdvanceController AdvanceController;
        internal readonly TrainUnitHashBuffer Hashes;

        public TrainTickContext()
        {
            State = new ClientTickState();
            Events = new TickEventBuffer(State);
            AdvanceController = new ClientTickAdvanceController(State, Events);
            Hashes = new TrainUnitHashBuffer(State);
        }

        internal void CompleteInitialSnapshot()
        {
            IsInitialSnapshotApplied = true;
        }

    }
}
