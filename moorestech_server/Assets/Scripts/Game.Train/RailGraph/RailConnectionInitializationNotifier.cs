using System;
using UniRx;

namespace Game.Train.RailGraph
{
    internal sealed class RailConnectionInitializationNotifier : IDisposable
    {
        public Subject<RailConnectionInitializationData> RailConnectionInitializedEvent { get; private set; }

        public RailConnectionInitializationNotifier()
        {
            RailConnectionInitializedEvent = new Subject<RailConnectionInitializationData>();
        }

        public void Notify(RailConnectionInitializationData data)
        {
            RailConnectionInitializedEvent?.OnNext(data);
        }

        public void Reset()
        {
            RailConnectionInitializedEvent?.Dispose();
            RailConnectionInitializedEvent = new Subject<RailConnectionInitializationData>();
        }

        public void Dispose()
        {
            RailConnectionInitializedEvent?.Dispose();
        }
    }
}
