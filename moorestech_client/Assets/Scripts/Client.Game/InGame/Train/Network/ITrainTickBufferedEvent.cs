namespace Client.Game.InGame.Train.Network
{
    // tick到達時に適用される列車イベントを表す。
    // Represents a buffered train event applied when its tick is reached.
    public interface ITrainTickBufferedEvent
    {
        void Apply();
    }
}
