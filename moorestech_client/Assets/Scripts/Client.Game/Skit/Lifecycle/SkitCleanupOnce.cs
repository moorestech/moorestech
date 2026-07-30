using System.Threading;

namespace Client.Game.Skit.Lifecycle
{
    public sealed class SkitCleanupOnce
    {
        private int _cleanupStarted;
        private int _mapPinHidden;

        public bool TryBegin()
        {
            return Interlocked.Exchange(ref _cleanupStarted, 1) == 0;
        }

        public void MarkMapPinHidden()
        {
            Interlocked.Exchange(ref _mapPinHidden, 1);
        }

        public bool TryTakeMapPinRestore()
        {
            return Interlocked.Exchange(ref _mapPinHidden, 0) == 1;
        }
    }
}
