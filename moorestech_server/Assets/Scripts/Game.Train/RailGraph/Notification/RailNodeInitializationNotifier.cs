using System;
using UniRx;
using UnityEngine;

namespace Game.Train.RailGraph.Notification
{
    /// <summary>
    ///     RailNode初期化イベントを発火するためのラッパー
    ///     Wrapper that exposes a subject for rail node initialization events
    /// </summary>
    public class RailNodeInitializationNotifier : IDisposable
    {
        private readonly IRailGraphDatastore _datastore;
        private Subject<RailNodeInitializationData> _railNodeInitialize;
        public IObservable<RailNodeInitializationData> RailNodeInitializedEvent => _railNodeInitialize;

        public RailNodeInitializationNotifier(IRailGraphDatastore datastore)
        {
            _datastore = datastore;
            _railNodeInitialize = new Subject<RailNodeInitializationData>();
        }

        public void Notify(int NodeId)
        {
            _datastore.TryGetRailNode(NodeId, out var node);
            if (node == null)
                return;
            _railNodeInitialize?.OnNext(
                new RailNodeInitializationData(
                    NodeId,
                    node.Guid,
                    node.ConnectionDestination,
                    node.FrontControlPoint.OriginalPosition,
                    node.FrontControlPoint.ControlPointPosition,
                    node.BackControlPoint.ControlPointPosition)
                );
        }

        public void Reset()
        {
            _railNodeInitialize?.Dispose();
            _railNodeInitialize = new Subject<RailNodeInitializationData>();
        }

        public void Dispose()
        {
            _railNodeInitialize?.Dispose();
        }

    }
}
