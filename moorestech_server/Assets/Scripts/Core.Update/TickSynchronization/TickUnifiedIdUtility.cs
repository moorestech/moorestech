namespace Core.Update.TickSynchronization
{
    public static class TickUnifiedIdUtility
    {
        public static ulong CreateTickUnifiedId(uint tick, uint tickSequenceId)
            => ((ulong)tick << 32) | tickSequenceId;
    }
}
