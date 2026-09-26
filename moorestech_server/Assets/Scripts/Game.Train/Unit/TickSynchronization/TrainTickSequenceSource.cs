using Core.Update.TickSynchronization;

namespace Game.Train.Unit
{
    public sealed class TrainTickSequenceSource
    {
        public readonly TickSequenceState Sequence = new();
    }
}
