using System;
using UniRx;

namespace Game.Train.RailGraph.Notification
{
    /// <summary>
    ///     RailConnectionが削除されたときに通知するラッパー
    ///     Emits events when a rail connection is removed from the graph
    /// </summary>
    public sealed class RailConnectionRemovalNotifier : IDisposable
    {
        private Subject<RailConnectionRemovalData> _railConnectionRemoved;
        // RailConnection削除イベントの公開窓口
        // Exposes the observable stream for rail connection removals
        public IObservable<RailConnectionRemovalData> RailConnectionRemovedEvent => _railConnectionRemoved.AsObservable();

        // Subjectを初期化して購読受付を開始
        // Initialize the subject to accept subscribers
        public RailConnectionRemovalNotifier()
        {
            _railConnectionRemoved = new Subject<RailConnectionRemovalData>();
        }

        // 接続削除情報を発行
        // Publish the removed connection payload
        public void Notify(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid)
        {
            var data = new RailConnectionRemovalData(fromNodeId, fromGuid, toNodeId, toGuid);
            _railConnectionRemoved?.OnNext(data);
        }

        // Subjectをリセット
        // Reset the subject for a clean subscription stream
        public void Reset()
        {
            _railConnectionRemoved?.Dispose();
            _railConnectionRemoved = new Subject<RailConnectionRemovalData>();
        }

        // Subjectを破棄
        // Dispose the subject to end notifications
        public void Dispose()
        {
            _railConnectionRemoved?.Dispose();
        }

    }
}
