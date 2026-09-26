using System;

namespace Client.Game.InGame.Train.Network.TickSynchronization
{
    // 起動境界で通常の初期化失敗と区別する
    // Distinguish snapshot failure from ordinary initialization failure at the startup boundary
    public sealed class TrainInitialSnapshotException : Exception
    {
        internal TrainInitialSnapshotException(string message, Exception innerException) : base(message, innerException) { }
    }
}
