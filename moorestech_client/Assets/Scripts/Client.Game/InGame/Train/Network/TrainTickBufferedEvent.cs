using System;

namespace Client.Game.InGame.Train.Network
{
    // delegateで適用処理を持つ汎用tickイベント。
    // Generic tick event that stores apply logic as a delegate.
    public sealed class TrainTickBufferedEvent : ITrainTickBufferedEvent
    {
        private readonly Action _applyAction;

        private TrainTickBufferedEvent(Action applyAction)
        {
            _applyAction = applyAction;
        }

        public static ITrainTickBufferedEvent Create(Action applyAction)
        {
            if (applyAction == null)
            {
                throw new ArgumentNullException(nameof(applyAction));
            }

            return new TrainTickBufferedEvent(applyAction);
        }

        public void Apply()
        {
            _applyAction();
        }
    }
}
