using System;
using System.Collections.Generic;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.View;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BeltTransport
{
    // 複製の全量と毎tickの差分に合わせて、走行中のアイテムの表示物が生成・移動・破棄されるか。buffer内と内部segmentは表示しない
    // Whether displayed objects of running items are created, moved and destroyed following the replica's full state and per-tick diffs; buffered and internal-segment items are not shown
    public class BeltItemObjectPresenterTest
    {
        private const int Cell = BeltConstants.ItemWidth;
        private const int Speed = 32;
        private const float Surface = BeltItemWorldPositionCalculator.SurfaceHeight;
        private static readonly ItemId ItemA = new(1);

        [Test]
        public void RunningItemsGetObjectsBufferedAndInternalOnesDoNot()
        {
            var factory = new FakeBeltItemObjectFactory();
            var cache = new BeltTransportClientCache();
            new BeltItemObjectPresenter(cache, factory).Initialize();

            // 直線に2個、分岐のbufferに1個、内部segmentに1個
            // Two items on a straight line, one in a branch buffer, one on an internal segment
            cache.ApplyFullState(new BeltTransportFullState(new[]
            {
                Line(Running(1, 2 * Cell), Running(2, 0)),
                BranchWithBufferItem(3),
                InternalWith(4)
            }));

            Assert.AreEqual(2, factory.Created.Count, "objects only for the running items on cells");
            AssertPosition(new Vector3(0.5f, Surface, 0.5f), factory.Created[1]);
            AssertPosition(new Vector3(0.5f, Surface, 2.5f), factory.Created[2]);
            Assert.AreEqual(ItemA, factory.CreatedItemIds[1], "created from the item kind");
        }

        [Test]
        public void TickMovesObjectsAndRemovedItemsDestroyThem()
        {
            var factory = new FakeBeltItemObjectFactory();
            var cache = new BeltTransportClientCache();
            new BeltItemObjectPresenter(cache, factory).Initialize();
            cache.ApplyFullState(new BeltTransportFullState(new[] { Line(Running(1, 2 * Cell), Running(2, 0)) }));

            // 1tickで先頭のアイテムは出口に留まり、後ろのアイテムは速度ぶん進む
            // In one tick the leading item stays at the exit and the trailing one advances by the speed
            cache.ApplyTickDiff(BeltTickDiff.Empty);
            AssertPosition(new Vector3(0.5f, Surface, 0.5f + Speed / (float)Cell), factory.Created[1]);
            AssertPosition(new Vector3(0.5f, Surface, 2.5f), factory.Created[2]);
            Assert.AreEqual(2, factory.Created.Count, "no new objects for a tick");

            // 組み直しで片方が消えた: その表示物だけ破棄され、残った個体は同じ表示物のまま
            // A re-assembly without one item: only its object is destroyed and the surviving instance keeps its object
            cache.ApplyFullState(new BeltTransportFullState(new[] { Line(Running(2, 0)) }));
            Assert.IsTrue(factory.Created[1].IsDestroyed, "vanished item destroyed");
            Assert.IsFalse(factory.Created[2].IsDestroyed, "surviving item kept");
            Assert.AreEqual(2, factory.Created.Count, "no re-creation across a re-assembly");
        }

        [Test]
        public void ObjectArrivingAfterTheItemVanishedIsDestroyedOnArrival()
        {
            var factory = new FakeBeltItemObjectFactory { Deferred = true };
            var cache = new BeltTransportClientCache();
            new BeltItemObjectPresenter(cache, factory).Initialize();
            cache.ApplyFullState(new BeltTransportFullState(new[] { Line(Running(1, 2 * Cell)) }));
            cache.ApplyFullState(new BeltTransportFullState(new[] { Line() }));

            // 読み込み完了が消滅の後に来た: 届いた表示物はその場で破棄
            // The load completes after the item vanished: the arriving object is destroyed on the spot
            factory.CompleteDeferred();
            Assert.IsTrue(factory.Created[1].IsDestroyed);
        }

        private static void AssertPosition(Vector3 expected, FakeBeltItemObject itemObject)
        {
            Assert.AreEqual(expected.x, itemObject.Position.x, 1e-4f);
            Assert.AreEqual(expected.y, itemObject.Position.y, 1e-4f);
            Assert.AreEqual(expected.z, itemObject.Position.z, 1e-4f);
        }

        // 原点から+Zへ3マスの直線
        // A three-cell straight line from the origin along +Z
        private static BeltSegmentState Line(params BeltItemSnapshot[] items)
        {
            var cells = new[] { FrontCell(0, 0, 0), FrontCell(0, 0, 1), FrontCell(0, 0, 2) };
            var shape = new BeltSegmentShape(BeltSegmentKind.Normal, false, cells, Speed, BeltDirection.Front, Array.Empty<BeltLinkShape>(), Array.Empty<BeltLinkShape>());
            return new BeltSegmentState(shape, 0, items, false, default);
        }

        private static BeltSegmentState BranchWithBufferItem(long instanceId)
        {
            var shape = new BeltSegmentShape(BeltSegmentKind.Branch, false, new[] { FrontCell(5, 0, 0) }, Speed, BeltDirection.Front, Array.Empty<BeltLinkShape>(), Array.Empty<BeltLinkShape>());
            return new BeltSegmentState(shape, 0, Array.Empty<BeltItemSnapshot>(), true, Running(instanceId, 0));
        }

        private static BeltSegmentState InternalWith(long instanceId)
        {
            var shape = new BeltSegmentShape(BeltSegmentKind.Normal, true, Array.Empty<BeltCellShape>(), Speed, BeltDirection.Front, Array.Empty<BeltLinkShape>(), Array.Empty<BeltLinkShape>());
            return new BeltSegmentState(shape, 0, new[] { Running(instanceId, Cell / 2) }, false, default);
        }

        private static BeltItemSnapshot Running(long instanceId, int distanceToExit)
        {
            return new BeltItemSnapshot(new BeltItem(ItemA, new ItemInstanceId(instanceId), BeltEntryDirection.FromBack), distanceToExit);
        }

        private static BeltCellShape FrontCell(int x, int y, int z)
        {
            return new BeltCellShape(new Vector3Int(x, y, z), BeltDirection.Front);
        }

        // 表示物を即時(または保留して後で)返し、個体IDごとに記録する。個体IDは生成順に1から振られている前提
        // Returns objects at once (or deferred until released) and records them by instance id; instance ids are assumed to be numbered from 1 in creation order
        private sealed class FakeBeltItemObjectFactory : IBeltItemObjectFactory
        {
            public readonly Dictionary<long, FakeBeltItemObject> Created = new();
            public readonly Dictionary<long, ItemId> CreatedItemIds = new();
            private readonly List<(UniTaskCompletionSource<IBeltItemObject> source, FakeBeltItemObject itemObject)> _deferred = new();
            public bool Deferred;

            public UniTask<IBeltItemObject> CreateAsync(ItemId itemId, Vector3 position)
            {
                var itemObject = new FakeBeltItemObject();
                var instanceId = Created.Count + 1;
                Created.Add(instanceId, itemObject);
                CreatedItemIds.Add(instanceId, itemId);
                if (!Deferred) return UniTask.FromResult<IBeltItemObject>(itemObject);
                var source = new UniTaskCompletionSource<IBeltItemObject>();
                _deferred.Add((source, itemObject));
                return source.Task;
            }

            public void CompleteDeferred()
            {
                foreach (var (source, itemObject) in _deferred) source.TrySetResult(itemObject);
                _deferred.Clear();
            }
        }

        private sealed class FakeBeltItemObject : IBeltItemObject
        {
            public Vector3 Position;
            public bool IsDestroyed;

            public void SetPosition(Vector3 position)
            {
                Position = position;
            }

            public void Destroy()
            {
                IsDestroyed = true;
            }
        }
    }
}
