using System.Threading;

namespace Client.Game.Skit.Lifecycle
{
    public sealed class SkitCleanupOnce
    {
        private int _cleanupStarted;

        public bool TryBegin()
        {
            return Interlocked.Exchange(ref _cleanupStarted, 1) == 0;
        }
    }
}
