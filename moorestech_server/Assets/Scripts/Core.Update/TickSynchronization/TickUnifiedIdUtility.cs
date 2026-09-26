namespace Core.Update.TickSynchronization
{
    public static class TrainTickUnifiedIdUtility
    {
        public static ulong CreateTickUnifiedId(uint tick, uint tickSequenceId)
            => ((ulong)tick << 32) | tickSequenceId;
    }
}
