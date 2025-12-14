using System;
using UniRx;

namespace Game.Train.RailGraph
{
    /// <summary>
    ///     RailNode蜿門ｾ励ゅｏ繧後◆繧翫∪縺吶・
    ///     Emits events when a rail node is removed from the graph
    /// </summary>
    public sealed class RailNodeRemovalNotifier : IDisposable
    {
        // RailNode削除イベントのSubjectを保持
        // Holds the subject that broadcasts node removal events
        public Subject<RailNodeRemovedData> RailNodeRemovedEvent { get; private set; }

        public RailNodeRemovalNotifier()
        {
            // Subjectを初期化して監視者を受け付ける
            // Initialize the subject so observers can subscribe
            RailNodeRemovedEvent = new Subject<RailNodeRemovedData>();
        }

        public void Notify(int nodeId, Guid nodeGuid)
        {
            // 現在のノード情報を購読者へ通知
            // Publish the removal information to subscribers
            RailNodeRemovedEvent?.OnNext(new RailNodeRemovedData(nodeId, nodeGuid));
        }

        public void Reset()
        {
            // 古いSubjectを破棄して新規に差し替え
            // Dispose the existing subject and create a new one
            RailNodeRemovedEvent?.Dispose();
            RailNodeRemovedEvent = new Subject<RailNodeRemovedData>();
        }

        public void Dispose()
        {
            // 監視を終了するためSubjectを破棄
            // Dispose the subject to stop further notifications
            RailNodeRemovedEvent?.Dispose();
        }

        public readonly struct RailNodeRemovedData
        {
            public RailNodeRemovedData(int nodeId, Guid nodeGuid)
            {
                NodeId = nodeId;
                NodeGuid = nodeGuid;
            }

            public int NodeId { get; }
            public Guid NodeGuid { get; }
        }
    }
}
