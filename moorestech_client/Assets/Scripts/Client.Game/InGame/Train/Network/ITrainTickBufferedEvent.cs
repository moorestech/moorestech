namespace Client.Game.InGame.Train.Network
{
    // tick到達時に適用されるstreamイベントを表す。
    // Represents a buffered stream event applied when its tick is reached.
    internal interface ITrainTickBufferedEvent
    {
        void Apply();
    }
}
