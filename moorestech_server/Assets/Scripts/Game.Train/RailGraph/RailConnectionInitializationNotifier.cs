using System;
using UniRx;

namespace Game.Train.RailGraph
{
    public class RailConnectionInitializationNotifier : IDisposable
    {
        private Subject<RailConnectionInitializationData> _railConnectionInitialized;
        // レール接続初期化イベント
        // Exposes the observable rail connection initialization stream
        public IObservable<RailConnectionInitializationData> RailConnectionInitializedEvent => _railConnectionInitialized.AsObservable();

        // 監視用Subject
        // Initialize the underlying subject used for notifications
        public RailConnectionInitializationNotifier()
        {
            _railConnectionInitialized = new Subject<RailConnectionInitializationData>();
        }

        // レールノードを参照して初期化結果を発行
        // Resolve rail nodes and publish the initialization payload
        public void Notify(int fromNodeId, int toNodeId, int distance)
        {
            RailGraphDatastore.TryGetRailNode(fromNodeId, out var fromNode);
            RailGraphDatastore.TryGetRailNode(toNodeId, out var toNode);
            if (fromNode == null || toNode == null) return;
            var data = new RailConnectionInitializationData(fromNodeId, fromNode.Guid, toNodeId, toNode.Guid, distance);
            _railConnectionInitialized?.OnNext(data);
        }

        // Subjectをリセットして新たな購読サイクルを開始
        // Reset the subject to start a fresh subscription cycle
        public void Reset()
        {
            _railConnectionInitialized?.Dispose();
            _railConnectionInitialized = new Subject<RailConnectionInitializationData>();
        }

        // 管理中のSubjectを破棄
        // Dispose the subject maintained by this notifier
        public void Dispose()
        {
            _railConnectionInitialized?.Dispose();
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
