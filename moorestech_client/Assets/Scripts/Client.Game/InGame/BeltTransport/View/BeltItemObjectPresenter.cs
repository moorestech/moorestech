using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.BeltTransport.View
{
    // 複製の変化を購読し、走行中のアイテムの表示物を生成・移動・破棄する。tick間の補間はしない
    // Subscribes to replica changes and creates, moves and destroys the displayed objects of running items; no interpolation between ticks
    // buffer内と、マスを持たない内部segment上のアイテムは表示しない(合流・分岐マスは箱で隠す前提)
    // Items inside a buffer or on a cell-less internal segment are not displayed (merge and branch cells are meant to be hidden by a box)
    public sealed class BeltItemObjectPresenter : IInitializable, IDisposable
    {
        private readonly BeltTransportClientCache _cache;
        private readonly IBeltItemObjectFactory _factory;
        private readonly Dictionary<ItemInstanceId, BeltItemView> _views = new();
        private readonly List<ItemInstanceId> _vanished = new();
        private int _refresh;
        private IDisposable _assembledSubscription;
        private IDisposable _tickedSubscription;

        public BeltItemObjectPresenter(BeltTransportClientCache cache, IBeltItemObjectFactory factory)
        {
            _cache = cache;
            _factory = factory;
        }

        public void Initialize()
        {
            _assembledSubscription = _cache.OnReplicaAssembled.Subscribe(_ => Refresh());
            _tickedSubscription = _cache.OnReplicaTicked.Subscribe(_ => Refresh());
        }

        // 複製の走行中アイテムを全て見て回り、見かけたものは位置を置き、見かけなかったものは消す。個体IDは組み直しを跨いで同じなので表示物は引き継がれる
        // Walk every running item in the replica, placing the ones seen and removing the ones not seen; instance ids survive a re-assembly, so objects carry over
        private void Refresh()
        {
            _refresh++;
            var replica = _cache.Replica;
            for (var segmentIndex = 0; segmentIndex < replica.Segments.Length; segmentIndex++)
            {
                if (replica.Shapes[segmentIndex].Cells.Length == 0) continue;
                foreach (var state in replica.Segments[segmentIndex].CaptureItems())
                {
                    var position = BeltItemWorldPositionCalculator.Calculate(replica.Shapes, segmentIndex, state.DistanceToExit, state.Item.EntryDirection);
                    Show(state.Item.ItemInstanceId, state.Item.ItemId, position);
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
                var created = new BeltItemView(_refresh, position);
                _views.Add(instanceId, created);
                _factory.CreateAsync(itemId, position).ContinueWith(created.Attach).Forget();
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

        public void Dispose()
        {
            _assembledSubscription?.Dispose();
            _tickedSubscription?.Dispose();
        }
    }
}
