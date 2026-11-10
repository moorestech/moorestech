using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Logic.Common;
using Core.Master;
using Game.Block.Interface.State;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.Family
{
    /// <summary>
    /// 追跡ブロックの液体の配信（パイプの複製の変化・液体プラットフォーム・閉・通知のたびに別の VM）
    /// Publishing the tracked block's fluids (pipe replica changes, fluid platform, closed, a distinct view model per notification)
    /// </summary>
    public class BlockFluidSlotsModelTest
    {
        private static readonly Vector3Int PipePosition = new(4, 0, 0);
        private static readonly Vector3Int OtherPosition = new(9, 0, 0);

        private TrackedBlockHarness _harness;
        private FakeFluidReplicaReader _replica;
        private BlockFluidSlotsModel _model;
        private readonly List<BlockFluidSlotsViewModel> _published = new();

        [SetUp]
        public void SetUp()
        {
            _harness = new TrackedBlockHarness();
            _replica = new FakeFluidReplicaReader();
            _model = new BlockFluidSlotsModel(_harness.Tracker, _replica);
            _model.Initialize();
            _model.Current.Subscribe(_published.Add);
            // 購読直後の現在値は数えない
            // Drop the initial value delivered on subscribe
            _published.Clear();
        }

        [TearDown]
        public void TearDown() => _harness.Dispose();

        [Test]
        public void 複製の量が変わるとパイプの行が描き直される()
        {
            _replica.SetNode(PipePosition, 3, 0, 100);
            _harness.Track(_harness.CreateBlock(PipePosition, null));
            _published.Clear();

            _replica.SetNode(PipePosition, 3, 40, 100);
            _replica.NotifyChanged();
            Assert.AreEqual(1, _published.Count);
            Assert.AreEqual(40, ((FilledBlockFluidSlotState)_model.Current.Value.Slots[0]).Amount, 1e-9);

            _replica.SetNode(PipePosition, 3, 60, 100);
            _replica.NotifyChanged();
            Assert.AreEqual(2, _published.Count);
            Assert.AreEqual(60, ((FilledBlockFluidSlotState)_model.Current.Value.Slots[0]).Amount, 1e-9);

            // 変わらないまま通知しても流れない
            // A notification without a change publishes nothing
            _replica.NotifyChanged();
            Assert.AreEqual(2, _published.Count);
        }

        [Test]
        public void 液体の種類だけが変わっても描き直される()
        {
            _replica.SetNode(PipePosition, 3, 40, 100);
            _harness.Track(_harness.CreateBlock(PipePosition, null));
            _published.Clear();

            _replica.SetNode(PipePosition, 5, 40, 100);
            _replica.NotifyChanged();

            Assert.AreEqual(1, _published.Count);
            Assert.AreEqual(new FluidId(5), ((FilledBlockFluidSlotState)_model.Current.Value.Slots[0]).FluidId);
        }

        [Test]
        public void 液体プラットフォームは入力から出力の列を配る()
        {
            var tanks = new FluidMachineInventoryStateDetail(new List<FluidMessagePack> { new(5, 10, 100) }, new List<FluidMessagePack> { new(7, 2, 50) });

            _harness.Track(_harness.CreateBlock(OtherPosition, null, (FluidMachineInventoryStateDetail.BlockStateDetailKey, tanks)));

            Assert.AreEqual(2, _model.Current.Value.Slots.Count);
            Assert.AreEqual(new FluidId(5), ((FilledBlockFluidSlotState)_model.Current.Value.Slots[0]).FluidId);
            Assert.IsFalse(_model.Current.Value.HasProgress);
        }

        [Test]
        public void 機械の進捗は機械の詳細から立つ()
        {
            var machine = new MachineBlockStateDetail(0.5f, System.Guid.Empty, System.Guid.Empty, false);

            _harness.Track(_harness.CreateBlock(OtherPosition, null, (MachineBlockStateDetail.BlockStateDetailKey, machine)));

            Assert.IsTrue(_model.Current.Value.HasProgress);
            Assert.AreEqual(0.5f, _model.Current.Value.Progress, 1e-6);
        }

        [Test]
        public void 貨車と閉は空の列で進捗なし()
        {
            _harness.Track(_harness.CreateBlock(PipePosition, null, (FluidMachineInventoryStateDetail.BlockStateDetailKey, new FluidMachineInventoryStateDetail(new List<FluidMessagePack> { new(5, 1, 10) }, new List<FluidMessagePack>()))));

            _harness.Track(null);

            Assert.AreEqual(0, _model.Current.Value.Slots.Count);
            Assert.IsFalse(_model.Current.Value.HasProgress);
        }

        [Test]
        public void パイプでないブロックを追跡している間は複製の通知を購読しない()
        {
            _harness.Track(_harness.CreateBlock(OtherPosition, null));
            _published.Clear();

            Assert.AreEqual(0, _replica.SubscriberCount);
            _replica.SetNode(OtherPosition, 3, 40, 100);
            _replica.NotifyChanged();

            Assert.AreEqual(0, _published.Count);
        }

        [Test]
        public void 追跡を替えるとパイプの購読を外す()
        {
            _replica.SetNode(PipePosition, 3, 0, 100);
            _harness.Track(_harness.CreateBlock(PipePosition, null));
            Assert.AreEqual(1, _replica.SubscriberCount);

            _harness.Track(null);

            Assert.AreEqual(0, _replica.SubscriberCount);
        }

        [Test]
        public void 追跡の状態変化のたびに中身が同じでも新しいVMが流れる()
        {
            AssertDistinctOnEachNotification();
            var tanks = new FluidMachineInventoryStateDetail(new List<FluidMessagePack> { new(5, 10, 100) }, new List<FluidMessagePack> { new(7, 2, 50) });
            AssertDistinctOnEachNotification((FluidMachineInventoryStateDetail.BlockStateDetailKey, tanks));

            #region Internal

            void AssertDistinctOnEachNotification(params (string key, object detail)[] details)
            {
                _harness.Track(_harness.CreateBlock(OtherPosition, null, details));
                var before = _model.Current.Value;
                _published.Clear();

                _harness.NotifyStateChanged();
                _harness.NotifyStateChanged();

                // 2 回とも流れ、3 つの値が互いに別の参照
                // Both notifications publish, and the three values are distinct references
                Assert.AreEqual(2, _published.Count);
                Assert.AreNotSame(before, _published[0]);
                Assert.AreNotSame(before, _published[1]);
                Assert.AreNotSame(_published[0], _published[1]);
            }

            #endregion
        }

        [Test]
        public void 液体の行は機械なら出さず_機械でなく1件以上で出す()
        {
            var empty = new BlockFluidSlotsViewModel(new List<BlockFluidSlotState>(), false, 0);
            var one = new BlockFluidSlotsViewModel(new List<BlockFluidSlotState> { BlockFluidSlotState.Empty(10) }, false, 0);

            Assert.IsFalse(FluidRowVisibility.Shows(one, true));
            Assert.IsFalse(FluidRowVisibility.Shows(empty, false));
            Assert.IsTrue(FluidRowVisibility.Shows(one, false));
        }

        // 手で流す通知と節を持つ偽の読み手
        // A fake reader with hand-driven notifications and nodes
        private sealed class FakeFluidReplicaReader : IFluidReplicaReader
        {
            private readonly Subject<Unit> _changed = new();
            private readonly Dictionary<Vector3Int, (int fluidId, double amount, double capacity)> _nodes = new();

            public int SubscriberCount { get; private set; }

            public System.IObservable<Unit> OnReplicaChanged => Observable.Create<Unit>(observer =>
            {
                SubscriberCount++;
                var subscription = _changed.Subscribe(observer);
                return Disposable.Create(() =>
                {
                    SubscriberCount--;
                    subscription.Dispose();
                });
            });

            public void SetNode(Vector3Int position, int fluidId, double amount, double capacity) => _nodes[position] = (fluidId, amount, capacity);

            public void NotifyChanged() => _changed.OnNext(Unit.Default);

            public bool TryReadNode(Vector3Int position, out int fluidId, out double amount, out double capacity)
            {
                var found = _nodes.TryGetValue(position, out var node);
                (fluidId, amount, capacity) = node;
                return found;
            }
        }
    }
}
