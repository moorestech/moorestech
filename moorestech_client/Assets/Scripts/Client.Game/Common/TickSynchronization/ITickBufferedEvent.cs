namespace Client.Game.Common.TickSynchronization
{
    // tick到達時に適用されるstreamイベントを表す。
    // Represents a buffered stream event applied when its tick is reached.
    internal interface ITickBufferedEvent
    {
        void Apply();
    }
}
