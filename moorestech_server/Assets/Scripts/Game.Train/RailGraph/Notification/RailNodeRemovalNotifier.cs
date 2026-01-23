using System;
using UniRx;

namespace Game.Train.RailGraph.Notification
{
    /// <summary>
    ///     RailNodeが削除されたときに通知するラッパー
    ///     Emits events when a rail node is removed from the graph
    /// </summary>
    public sealed class RailNodeRemovalNotifier : IDisposable
    {
        private Subject<RailNodeRemovedData> _railNodeRemoved;
        // RailNode削除イベントの公開窓口
        // Exposes the observable stream for rail node removals
        public IObservable<RailNodeRemovedData> RailNodeRemovedEvent => _railNodeRemoved.AsObservable();

        // Subjectを初期化して購読受付を開始
        // Initialize the subject so observers can subscribe
        public RailNodeRemovalNotifier()
        {
            _railNodeRemoved = new Subject<RailNodeRemovedData>();
        }

        // ノード削除情報を発行
        // Publish the removed node payload
        public void Notify(int nodeId, Guid nodeGuid)
        {
            var data = new RailNodeRemovedData(nodeId, nodeGuid);
            _railNodeRemoved?.OnNext(data);
        }

        // Subjectをリセット
        // Reset the subject for a fresh subscription cycle
        public void Reset()
        {
            _railNodeRemoved?.Dispose();
            _railNodeRemoved = new Subject<RailNodeRemovedData>();
        }

        // Subjectを破棄
        // Dispose the subject to stop notifications
        public void Dispose()
        {
            _railNodeRemoved?.Dispose();
        }

    }
}
