using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.Network;
using Client.Game.TickSynchronization;

namespace Client.Game.InGame.Train.Network.TickSynchronization
{
    // train/rail stream専用の同期状態を所有する。
    // Own synchronization state exclusively for the train/rail stream.
    public sealed class TrainTickContext
    {
        internal bool IsInitialSnapshotApplied { get; private set; }
        public readonly TrainUnitTickState State;
        public readonly TrainUnitFutureMessageBuffer Events;
        internal readonly ClientTickAdvanceController AdvanceController;
        public readonly TrainUnitHashBuffer Hashes;

        public TrainTickContext()
        {
            State = new TrainUnitTickState();
            Events = new TrainUnitFutureMessageBuffer(State);
            AdvanceController = new ClientTickAdvanceController(State, Events);
            Hashes = new TrainUnitHashBuffer(State);
        }

        internal void CompleteInitialSnapshot()
        {
            IsInitialSnapshotApplied = true;
        }

    }
}
