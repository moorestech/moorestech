using System;

namespace Client.Game.Common.TickSynchronization
{
    // delegateで適用処理を持つ汎用tickイベント。
    // Generic tick event that stores apply logic as a delegate.
    internal sealed class TickBufferedEvent : ITickBufferedEvent
    {
        private readonly Action _applyAction;

        private TickBufferedEvent(Action applyAction)
        {
            _applyAction = applyAction;
        }

        public static ITickBufferedEvent Create(Action applyAction)
        {
            if (applyAction == null)
            {
                throw new ArgumentNullException(nameof(applyAction));
            }

            return new TickBufferedEvent(applyAction);
        }

        public void Apply()
        {
            _applyAction();
        }
    }
}
