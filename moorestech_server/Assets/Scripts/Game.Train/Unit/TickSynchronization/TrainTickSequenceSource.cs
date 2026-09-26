using Core.Update.TickSynchronization;

namespace Game.Train.Unit.TickSynchronization
{
    public sealed class TrainTickSequenceSource
    {
        public readonly TickSequenceState Sequence = new();
    }
}
