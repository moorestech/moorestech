using System;

namespace Client.Game.InGame.Train.Network
{
    // delegateで適用処理を持つ汎用tickイベント。
    // Generic tick event that stores apply logic as a delegate.
    public sealed class TrainTickBufferedEvent : ITrainTickBufferedEvent
    {
        private readonly Action _applyAction;
        public string EventTag { get; }

        private TrainTickBufferedEvent(string eventTag, Action applyAction)
        {
            EventTag = eventTag;
            _applyAction = applyAction;
        }

        public static ITrainTickBufferedEvent Create(string eventTag, Action applyAction)
        {
            if (string.IsNullOrWhiteSpace(eventTag))
            {
                throw new ArgumentException("eventTag must not be null or empty.", nameof(eventTag));
            }
            if (applyAction == null)
            {
                throw new ArgumentNullException(nameof(applyAction));
            }

            return new TrainTickBufferedEvent(eventTag, applyAction);
        }

        public void Apply()
        {
            _applyAction();
        }
    }
}
