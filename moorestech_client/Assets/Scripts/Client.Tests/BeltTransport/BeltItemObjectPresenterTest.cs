using System;
using System.Collections.Generic;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.View;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
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
        private const float Surface = 0.35f;
        private static readonly ItemId ItemA = new(1);

        [Test]
        public void RunningItemsGetObjectsBufferedAndInternalOnesDoNot()
        {
            var factory = new FakeBeltItemObjectFactory();
            var cache = new BeltTransportClientCache();
            var presenter = new BeltItemObjectPresenter(cache, factory, new FakeBeltItemViewpoint(Vector3.zero));

            // 直線に2個、分岐のbufferに1個、内部segmentに1個
            // Two items on a straight line, one in a branch buffer, one on an internal segment
            cache.ApplyFullState(new BeltTransportFullState(new[]
            {
                Line(Running(1, 2 * Cell), Running(2, 0)),
                BranchWithBufferItem(3),
                InternalWith(4)
            }));
            presenter.UpdateAll();

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
            var presenter = new BeltItemObjectPresenter(cache, factory, new FakeBeltItemViewpoint(Vector3.zero));
            cache.ApplyFullState(new BeltTransportFullState(new[] { Line(Running(1, 2 * Cell), Running(2, 0)) }));
            presenter.UpdateAll();

            // 1tickで先頭のアイテムは出口に留まり、後ろのアイテムは速度ぶん進む
            // In one tick the leading item stays at the exit and the trailing one advances by the speed
            cache.ApplyTickDiff(BeltTickDiff.Empty);
            presenter.UpdateAll();
            AssertPosition(new Vector3(0.5f, Surface, 0.5f + Speed / (float)Cell), factory.Created[1]);
            AssertPosition(new Vector3(0.5f, Surface, 2.5f), factory.Created[2]);
            Assert.AreEqual(2, factory.Created.Count, "no new objects for a tick");

            // 組み直しで片方が消えた: その表示物だけ破棄され、残った個体は同じ表示物のまま
            // A re-assembly without one item: only its object is destroyed and the surviving instance keeps its object
            cache.ApplyFullState(new BeltTransportFullState(new[] { Line(Running(2, 0)) }));
            presenter.UpdateAll();
            Assert.IsTrue(factory.Created[1].IsDestroyed, "vanished item destroyed");
            Assert.IsFalse(factory.Created[2].IsDestroyed, "surviving item kept");
            Assert.AreEqual(2, factory.Created.Count, "no re-creation across a re-assembly");
        }

        [Test]
        public void UpdateAllDoesNothingUntilTheReplicaAdvances()
        {
            var factory = new FakeBeltItemObjectFactory();
            var cache = new BeltTransportClientCache();
            var presenter = new BeltItemObjectPresenter(cache, factory, new FakeBeltItemViewpoint(Vector3.zero));

            // 複製が無い間は何もしない
            // Nothing happens while there is no replica yet
            presenter.UpdateAll();
            Assert.AreEqual(0, factory.Created.Count);

            // 同じ版のまま何度呼んでも、表示物は作り直されず動かない
            // Repeated calls on the same version neither re-create nor move the objects
            cache.ApplyFullState(new BeltTransportFullState(new[] { Line(Running(1, 2 * Cell)) }));
            presenter.UpdateAll();
            factory.Created[1].Position = Vector3.one;
            presenter.UpdateAll();
            Assert.AreEqual(1, factory.Created.Count);
            Assert.AreEqual(Vector3.one, factory.Created[1].Position, "untouched while the replica version is unchanged");
        }

        // 基準点から遠い segment のアイテムは具現化せず、近づけば作り、入る側と出る側の間では状態を保ち、離れれば即破棄する(ADR 0085)
        // Items on a segment far from the viewpoint are not materialized; they are created when it comes near, kept between the enter and leave distances, and destroyed at once when it moves away (ADR 0085)
        [Test]
        public void OnlySegmentsWithinRangeOfTheViewpointGetObjects()
        {
            var factory = new FakeBeltItemObjectFactory();
            var cache = new BeltTransportClientCache();
            var viewpoint = new FakeBeltItemViewpoint(new Vector3(0.5f, 0f, 1000f));
            var presenter = new BeltItemObjectPresenter(cache, factory, viewpoint);

            // 原点の直線(マス中心 z=0.5..2.5)。基準点は z=1000 で範囲外
            // A line at the origin (cell centers z = 0.5..2.5); the viewpoint at z = 1000 is out of range
            cache.ApplyFullState(new BeltTransportFullState(new[] { Line(Running(1, 2 * Cell), Running(2, 0)) }));
            presenter.UpdateAll();
            Assert.AreEqual(0, factory.Created.Count, "nothing materialized out of range");

            // 入る側(340 m)の内側へ: 作られる
            // Inside the enter distance (340 m): created
            viewpoint.Position = new Vector3(0.5f, 0f, 1.5f + 330f);
            cache.ApplyTickDiff(BeltTickDiff.Empty);
            presenter.UpdateAll();
            Assert.AreEqual(2, factory.Created.Count, "materialized once within the enter distance");

            // 入る側と出る側の間(345 m 前後): 内側のまま
            // Between the enter and leave distances (about 345 m): stays inside
            viewpoint.Position = new Vector3(0.5f, 0f, 1.5f + 346f);
            cache.ApplyTickDiff(BeltTickDiff.Empty);
            presenter.UpdateAll();
            Assert.IsFalse(factory.Created[1].IsDestroyed, "kept between the enter and leave distances");

            // 出る側(350 m)の外へ: 即破棄
            // Beyond the leave distance (350 m): destroyed at once
            viewpoint.Position = new Vector3(0.5f, 0f, 1.5f + 360f);
            cache.ApplyTickDiff(BeltTickDiff.Empty);
            presenter.UpdateAll();
            Assert.IsTrue(factory.Created[1].IsDestroyed && factory.Created[2].IsDestroyed, "destroyed once beyond the leave distance");

            // 戻ると別の表示物として作り直される(プールなし)
            // Coming back re-creates them as new objects (no pool)
            viewpoint.Position = new Vector3(0.5f, 0f, 1.5f);
            cache.ApplyTickDiff(BeltTickDiff.Empty);
            presenter.UpdateAll();
            Assert.AreEqual(4, factory.Created.Count, "re-created on return");
        }

        // 基準点が無い間(カメラ未生成)は何も具現化しない
        // Nothing is materialized while there is no viewpoint (camera not created yet)
        [Test]
        public void NothingIsMaterializedWithoutAViewpoint()
        {
            var factory = new FakeBeltItemObjectFactory();
            var cache = new BeltTransportClientCache();
            var viewpoint = new FakeBeltItemViewpoint(Vector3.zero) { HasPosition = false };
            var presenter = new BeltItemObjectPresenter(cache, factory, viewpoint);
            cache.ApplyFullState(new BeltTransportFullState(new[] { Line(Running(1, 2 * Cell)) }));
            presenter.UpdateAll();
            Assert.AreEqual(0, factory.Created.Count);

            viewpoint.HasPosition = true;
            cache.ApplyTickDiff(BeltTickDiff.Empty);
            presenter.UpdateAll();
            Assert.AreEqual(1, factory.Created.Count, "materialized once the viewpoint exists");
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
            var shape = new BeltSegmentShape(BeltSegmentKind.Normal, false, cells, cells.Length, Speed, BeltDirection.Front, Array.Empty<BeltLinkShape>(), Array.Empty<BeltLinkShape>());
            return new BeltSegmentState(shape, 0, Speed, items, false, default);
        }

        private static BeltSegmentState BranchWithBufferItem(long instanceId)
        {
            var shape = new BeltSegmentShape(BeltSegmentKind.Branch, false, new[] { FrontCell(5, 0, 0) }, 1, Speed, BeltDirection.Front, Array.Empty<BeltLinkShape>(), Array.Empty<BeltLinkShape>());
            return new BeltSegmentState(shape, 0, Speed, Array.Empty<BeltItemSnapshot>(), true, Running(instanceId, 0));
        }

        private static BeltSegmentState InternalWith(long instanceId)
        {
            var shape = new BeltSegmentShape(BeltSegmentKind.Normal, true, Array.Empty<BeltCellShape>(), 1, Speed, BeltDirection.Front, Array.Empty<BeltLinkShape>(), Array.Empty<BeltLinkShape>());
            return new BeltSegmentState(shape, 0, Speed, new[] { Running(instanceId, Cell / 2) }, false, default);
        }

        private static BeltItemSnapshot Running(long instanceId, int distanceToExit)
        {
            return new BeltItemSnapshot(new BeltItem(ItemA, new ItemInstanceId(instanceId), BeltEntryDirection.FromBack), distanceToExit);
        }

        private static BeltCellShape FrontCell(int x, int y, int z)
        {
            return new BeltCellShape(new Vector3Int(x, y, z), BeltDirection.Front);
        }

        // 表示物をその場で返し、個体IDごとに記録する。個体IDは生成順に1から振られている前提
        // Returns objects on the spot and records them by instance id; instance ids are assumed to be numbered from 1 in creation order
        private sealed class FakeBeltItemObjectFactory : IBeltItemObjectFactory
        {
            public readonly Dictionary<long, FakeBeltItemObject> Created = new();
            public readonly Dictionary<long, ItemId> CreatedItemIds = new();

            public IBeltItemObject Create(ItemId itemId, Vector3 position)
            {
                var itemObject = new FakeBeltItemObject { Position = position };
                var instanceId = Created.Count + 1;
                Created.Add(instanceId, itemObject);
                CreatedItemIds.Add(instanceId, itemId);
                return itemObject;
            }
        }

        private sealed class FakeBeltItemViewpoint : IBeltItemViewpoint
        {
            public Vector3 Position;
            public bool HasPosition = true;

            public FakeBeltItemViewpoint(Vector3 position)
            {
                Position = position;
            }

            public bool TryGetPosition(out Vector3 position)
            {
                position = Position;
                return HasPosition;
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
