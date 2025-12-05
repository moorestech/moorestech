using System;
using System.Collections.Generic;
using Game.Train.Common;
using UniRx;

namespace Game.Train.RailGraph
{
    /// <summary>
    /// レールグラフ更新通知専用クラス。
    /// RailNode から RailComponentID を解決するためのデリゲートを受け取り、
    /// 対象ノードの変更に応じて RailGraphUpdateEvent を発火する。
    /// </summary>
    internal sealed class RailGraphNotifier : IDisposable
    {
        /// <summary>
        /// レールグラフ更新イベント。
        /// 変更があった RailComponentID のリストを流す。
        /// </summary>
        public Subject<List<RailComponentID>> RailGraphUpdateEvent { get; private set; }

        // RailNode から RailComponentID を解決するためのデリゲート
        private readonly Func<RailNode, (bool success, RailComponentID id)> _resolveRailComponentId;

        public RailGraphNotifier(Func<RailNode, (bool success, RailComponentID id)> resolveRailComponentId)
        {
            _resolveRailComponentId = resolveRailComponentId
                ?? throw new ArgumentNullException(nameof(resolveRailComponentId));

            RailGraphUpdateEvent = new Subject<List<RailComponentID>>();
        }

        /// <summary>
        /// 2つの RailNode 間で接続状態が更新されたときに呼び出す。
        /// 関連する RailComponentID を解決し、イベントを発火する。
        /// </summary>
        public void NotifyRailGraphUpdate(RailNode node1, RailNode node2)
        {
            var changedComponentIds = new List<RailComponentID>(capacity: 2);

            AddIfResolved(changedComponentIds, node1);
            AddIfResolved(changedComponentIds, node2);

            if (changedComponentIds.Count > 0)
            {
                RailGraphUpdateEvent.OnNext(changedComponentIds);
            }
        }

        /// <summary>
        /// Datastore リセット時などに Subject を作り直す。
        /// </summary>
        public void Reset()
        {
            RailGraphUpdateEvent?.Dispose();
            RailGraphUpdateEvent = new Subject<List<RailComponentID>>();
        }

        private void AddIfResolved(List<RailComponentID> list, RailNode node)
        {
            if (node == null) return;

            var (success, id) = _resolveRailComponentId(node);
            if (success)
            {
                list.Add(id);
            }
        }

        public void Dispose()
        {
            RailGraphUpdateEvent?.Dispose();
        }
    }
}
