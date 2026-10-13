using System.Collections.Generic;
using Client.Starter.EventMode;

namespace Client.Tests.EventMode
{
    internal sealed class RecordingEventModeAutoStartActions : IEventModeAutoStartActions
    {
        internal readonly List<string> Calls = new();
        internal float QuitDeadlineSeconds;

        public void StartFreshWorld() => Calls.Add("start");

        public void QuitBecauseTitleGatesTimedOut(float deadlineSeconds)
        {
            QuitDeadlineSeconds = deadlineSeconds;
            Calls.Add("quit");
        }
    }
}
