using System;
using UniRx;

namespace Game.Train.RailGraph
{
    public class RailConnectionInitializationNotifier : IDisposable
    {
        public Subject<RailConnectionInitializationData> RailConnectionInitializedEvent { get; private set; }

        public RailConnectionInitializationNotifier()
        {
            RailConnectionInitializedEvent = new Subject<RailConnectionInitializationData>();
        }

        public void Notify(int fromNodeId, int toNodeId, int distance)
        {
            RailGraphDatastore.TryGetRailNode(fromNodeId, out var fromNode);
            RailGraphDatastore.TryGetRailNode(toNodeId, out var toNode);
            if (fromNode == null || toNode == null)
                return;
            RailConnectionInitializedEvent?.OnNext(
                new RailConnectionInitializationData(
                fromNodeId,
                fromNode.Guid,
                toNodeId,
                toNode.Guid,
                distance)
                );
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

        public readonly struct RailConnectionInitializationData
        {
            public RailConnectionInitializationData(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid, int distance)
            {
                FromNodeId = fromNodeId;
                FromGuid = fromGuid;
                ToNodeId = toNodeId;
                ToGuid = toGuid;
                Distance = distance;
            }

            public int FromNodeId { get; }
            public Guid FromGuid { get; }
            public int ToNodeId { get; }
            public Guid ToGuid { get; }
            public int Distance { get; }
        }
    }
}
