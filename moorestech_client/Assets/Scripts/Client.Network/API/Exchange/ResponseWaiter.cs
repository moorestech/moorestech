using System;
using UniRx;

namespace Client.Network.API
{
    public sealed class ResponseWaiter
    {
        public ResponseWaiter(Subject<(byte[] data, PacketWaitCompletionReason reason)> waitSubject, int timeoutSeconds)
        {
            WaitSubject = waitSubject;
            TimeoutSeconds = timeoutSeconds;
            SendTime = DateTime.Now;
        }

        // UIの期限とは独立した待機は期限切れにしない
        // A wait independent of the UI deadline does not expire
        public bool HasExpired(DateTime now) => TimeoutSeconds > 0 && (now - SendTime).TotalSeconds >= TimeoutSeconds;

        public Subject<(byte[] data, PacketWaitCompletionReason reason)> WaitSubject { get; }
        public int TimeoutSeconds { get; }
        public DateTime SendTime { get; }
    }
}
