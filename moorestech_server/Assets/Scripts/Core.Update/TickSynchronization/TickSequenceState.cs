namespace Core.Update.TickSynchronization
{
    public sealed class TickSequenceState
    {
        public uint Tick { get; private set; }
        public uint SequenceId { get; private set; }

        public void BeginTick(uint tick)
        {
            Tick = tick;
            SequenceId = 0;
        }

        public uint NextSequenceId() => ++SequenceId;
    }
}
