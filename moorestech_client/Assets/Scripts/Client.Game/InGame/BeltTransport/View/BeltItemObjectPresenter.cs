using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Sync.Replica;
using UnityEngine;

namespace Client.Game.InGame.BeltTransport.View
{
    // 複製が進んだ回だけ、走行中のアイテムの表示物を生成・移動・破棄する。tick間の補間はしない
    // Whenever the replica has advanced, creates, moves and destroys the displayed objects of running items; no interpolation between ticks
    // 呼び出しは購読でなく、列車の描画更新と同じ場所(TrainUnitClientSimulator)から毎フレーム明示的に行う
    // Not driven by a subscription: TrainUnitClientSimulator calls this every frame in the same explicit order as the train visuals
    // buffer内と、マスを持たない内部segment上のアイテムは表示しない(合流マスは同じファミリーの分岐器モデルで隠す前提)
    // Items inside a buffer or on a cell-less internal segment are not displayed (merge cells are hidden by the family's splitter model)
    // 具現化するのは基準点(カメラ)から範囲内の segment 上のアイテムだけ。複製は全体を持ち、表示物の数だけを絞る(ADR 0085)
    // Only items on segments within range of the viewpoint (camera) are materialized; the replica stays whole and only the object count is bounded (ADR 0085)
    public sealed class BeltItemObjectPresenter
    {
        private readonly BeltTransportClientCache _cache;
        private readonly IBeltItemObjectFactory _factory;
        private readonly IBeltItemViewpoint _viewpoint;
        private readonly Dictionary<ItemInstanceId, BeltItemView> _views = new();
        private readonly List<ItemInstanceId> _vanished = new();
        private int _refresh;
        private int _shownReplicaVersion;

        // 範囲の中心と半径は複製の組み立てごとに作り直す。同じ複製のあいだはヒステリシスの状態を持ち越す
        // The range centers and radii are rebuilt per replica assembly; the hysteresis state carries over while the replica is the same
        private BeltTransportReplica _rangeReplica;
        private BeltSegmentVisibleRange _range;

        public BeltItemObjectPresenter(BeltTransportClientCache cache, IBeltItemObjectFactory factory, IBeltItemViewpoint viewpoint)
        {
            _cache = cache;
            _factory = factory;
            _viewpoint = viewpoint;
        }

        // 複製の版が前回の表示から変わっていなければ何もしない
        // Does nothing while the replica version is unchanged since the last display
        public void UpdateAll()
        {
            if (_cache.Replica == null || _cache.ReplicaVersion == _shownReplicaVersion) return;
            _shownReplicaVersion = _cache.ReplicaVersion;
            Refresh();
        }

        // 範囲内の segment の走行中アイテムを見て回り、見かけたものは位置を置き、見かけなかったものは消す。個体IDは組み直しを跨いで同じなので表示物は引き継がれる
        // Walk the running items on segments within range, placing the ones seen and removing the ones not seen; instance ids survive a re-assembly, so objects carry over
        // 範囲外へ出た segment のアイテムは「見かけなかった」扱いで即破棄し、戻れば作り直す(ユーザー裁定 2026-10-18: プールなし)
        // Items on a segment that left the range count as not seen and are destroyed at once; they are re-created when it returns (user ruling 2026-10-18: no pool)
        private void Refresh()
        {
            _refresh++;
            var replica = _cache.Replica;
            if (!ReferenceEquals(_rangeReplica, replica))
            {
                _rangeReplica = replica;
                _range = new BeltSegmentVisibleRange(replica.Shapes);
            }

            // 基準点が無い間(カメラ未生成)は何も具現化しない
            // While there is no viewpoint yet (camera not created) nothing is materialized
            if (_viewpoint.TryGetPosition(out var viewpoint))
            {
                for (var segmentIndex = 0; segmentIndex < replica.Segments.Length; segmentIndex++)
                {
                    if (replica.Shapes[segmentIndex].Cells.Length == 0) continue;
                    if (!_range.Contains(segmentIndex, viewpoint)) continue;
                    foreach (var state in replica.Segments[segmentIndex].EnumerateItems())
                    {
                        var position = BeltItemWorldPositionCalculator.Calculate(replica.Shapes, segmentIndex, state.DistanceToExit, state.Item.EntryDirection);
                        Show(state.Item.ItemInstanceId, state.Item.ItemId, position);
                    }
                }
            }
            RemoveVanished();
            return;

            #region Internal

            void Show(ItemInstanceId instanceId, ItemId itemId, Vector3 position)
            {
                if (_views.TryGetValue(instanceId, out var view))
                {
                    view.MoveTo(_refresh, position);
                    return;
                }
                _views.Add(instanceId, new BeltItemView(_refresh, _factory.Create(itemId, position)));
            }

            void RemoveVanished()
            {
                _vanished.Clear();
                foreach (var pair in _views)
                {
                    if (pair.Value.SeenInRefresh != _refresh) _vanished.Add(pair.Key);
                }
                foreach (var instanceId in _vanished)
                {
                    _views[instanceId].Remove();
                    _views.Remove(instanceId);
                }
            }

            #endregion
        }
    }
}
