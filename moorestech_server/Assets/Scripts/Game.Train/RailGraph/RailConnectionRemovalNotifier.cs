using System;
using UniRx;

namespace Game.Train.RailGraph
{
    /// <summary>
    ///     RailConnection蜿門ｾ励ゅｏ繧後◆繧翫∪縺吶・
    ///     Emits events when a rail connection is removed from the graph
    /// </summary>
    public sealed class RailConnectionRemovalNotifier : IDisposable
    {
        // RailConnection削除イベントのSubject
        // Subject that broadcasts connection removal events
        public Subject<RailConnectionRemovalData> RailConnectionRemovedEvent { get; private set; }

        public RailConnectionRemovalNotifier()
        {
            // Subject初期化で購読を受け付け
            // Initialize the subject for subscriptions
            RailConnectionRemovedEvent = new Subject<RailConnectionRemovalData>();
        }

        public void Notify(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid)
        {
            // 削除対象の接続情報を通知
            // Publish the removed connection information
            RailConnectionRemovedEvent?.OnNext(new RailConnectionRemovalData(fromNodeId, fromGuid, toNodeId, toGuid));
        }

        public void Reset()
        {
            // Subjectを破棄して新しい購読ストリームへ切替
            // Dispose the subject and replace it with a fresh one
            RailConnectionRemovedEvent?.Dispose();
            RailConnectionRemovedEvent = new Subject<RailConnectionRemovalData>();
        }

        public void Dispose()
        {
            // イベント停止のためSubject破棄
            // Dispose the subject to stop broadcasting
            RailConnectionRemovedEvent?.Dispose();
        }

        public readonly struct RailConnectionRemovalData
        {
            public RailConnectionRemovalData(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid)
            {
                FromNodeId = fromNodeId;
                FromGuid = fromGuid;
                ToNodeId = toNodeId;
                ToGuid = toGuid;
            }

            public int FromNodeId { get; }
            public Guid FromGuid { get; }
            public int ToNodeId { get; }
            public Guid ToGuid { get; }
        }
    }
}
