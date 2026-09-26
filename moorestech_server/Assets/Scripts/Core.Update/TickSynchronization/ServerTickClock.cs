namespace Core.Update.TickSynchronization
{
    public sealed class ServerTickClock
    {
        public uint Tick { get; private set; }
        public void AdvanceTick() => Tick++;
    }
}
