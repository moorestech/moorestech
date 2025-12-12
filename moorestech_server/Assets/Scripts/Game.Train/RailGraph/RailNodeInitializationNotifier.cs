using System;
using UniRx;

namespace Game.Train.RailGraph
{
    /// <summary>
    ///     RailNode初期化イベントを発火するためのラッパー
    ///     Wrapper that exposes a subject for rail node initialization events
    /// </summary>
    internal sealed class RailNodeInitializationNotifier : IDisposable
    {
        public Subject<RailNodeInitializationData> RailNodeInitializedEvent { get; private set; }

        public RailNodeInitializationNotifier()
        {
            RailNodeInitializedEvent = new Subject<RailNodeInitializationData>();
        }

        public void Notify(RailNodeInitializationData data)
        {
            RailNodeInitializedEvent?.OnNext(data);
        }

        public void Reset()
        {
            RailNodeInitializedEvent?.Dispose();
            RailNodeInitializedEvent = new Subject<RailNodeInitializationData>();
        }

        public void Dispose()
        {
            RailNodeInitializedEvent?.Dispose();
        }
    }
}
