using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Client.Tests.Ui.State.BlockInventory.PanelModel
{
    public class BlockInventoryPanelLayoutModelTest
    {
        private sealed class FakeRule : IBlockInventoryLargePanelRule
        {
            public readonly Subject<Unit> Changed = new();
            public bool Large;
            public bool IsLargePanel(OpenBlockInventoryPanel panel) => Large;
            public IObservable<Unit> OnRuleInputChanged => Changed;
        }

        [SetUp]
        public void SetUp()
        {
            // BlockInventoryTestPanels がマスタのチェストを引くので、先にマスタを読む
            // BlockInventoryTestPanels looks up the master chest, so load the master first
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 閉じていれば小で列車は大()
        {
            var state = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed);
            var model = new BlockInventoryPanelLayoutModel(new FakePanelSource(state), Array.Empty<IBlockInventoryLargePanelRule>());
            model.Initialize();
            Assert.IsFalse(model.Current.Value.Layout.IsLarge);
            state.Value = BlockInventoryPanelStateFactory.FromTrain(1, Array.Empty<SlotItem>(), null);
            Assert.IsTrue(model.Current.Value.Layout.IsLarge);
            Assert.IsFalse(model.Current.Value.Layout.FillsPanelHeight);
        }

        [Test]
        public void 寄与のどれかが真なら大で高さ確定し入力の変化で引き直す()
        {
            var state = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed);
            var rule = new FakeRule();
            var model = new BlockInventoryPanelLayoutModel(new FakePanelSource(state), new IBlockInventoryLargePanelRule[] { new FakeRule(), rule });
            model.Initialize();
            state.Value = BlockInventoryTestPanels.Chest();
            Assert.IsFalse(model.Current.Value.Layout.IsLarge);
            rule.Large = true;
            rule.Changed.OnNext(Unit.Default);
            Assert.IsTrue(model.Current.Value.Layout.IsLarge);
            Assert.IsTrue(model.Current.Value.Layout.FillsPanelHeight);
            state.Value = BlockInventoryTestPanels.OpenFailedChest();
            Assert.IsFalse(model.Current.Value.Layout.IsLarge);   // 取得失敗は寄与が真でも小型（Web と同じ。D-60）
        }

        [Test]
        public void パネルの変化1回につき状態と大きさの組が1回だけ流れその状態を運ぶ()
        {
            // 画面は派生元と派生先を同時に購読しないので、途中の組（旧い状態と新しい大きさ）が流れない（D-55）
            // The screen never subscribes to both source and derived value, so no intermediate pair (old state, new size) flows (D-55)
            var state = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed);
            var model = new BlockInventoryPanelLayoutModel(new FakePanelSource(state), Array.Empty<IBlockInventoryLargePanelRule>());
            model.Initialize();
            var received = new List<BlockInventoryPanelPresentation>();
            model.Current.Skip(1).Subscribe(received.Add);
            var train = BlockInventoryPanelStateFactory.FromTrain(1, Array.Empty<SlotItem>(), null);
            state.Value = train;
            Assert.AreEqual(1, received.Count);
            Assert.AreSame(train, received[0].State);
            Assert.IsTrue(received[0].Layout.IsLarge);
        }
    }
}
