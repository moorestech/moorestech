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

        public Subject<(byte[] data, PacketWaitCompletionReason reason)> WaitSubject { get; }
        public int TimeoutSeconds { get; }
        public DateTime SendTime { get; }
    }
}
