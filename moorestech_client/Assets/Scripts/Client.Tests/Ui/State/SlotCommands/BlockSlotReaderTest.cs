using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.SlotCommands.Block;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Client.Tests.Ui.State.SlotCommands
{
    /// <summary>
    /// 開閉はパネルの状態から、枠の値は生の値から読む（D-37）
    /// Open/closed comes from the panel state, slot values from the raw source (D-37)
    /// </summary>
    public class BlockSlotReaderTest
    {
        private ItemId _wood;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _wood = MasterHolder.ItemMaster.GetItemId(System.Guid.Parse("00000000-0000-0000-1234-000000000001"));
        }

        [Test]
        public void イベント無しの先書き後はパネルの枠でなく生の値を返す()
        {
            var panel = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.ChestWithSlots(default(SlotItem))));
            var reader = new BlockSlotReader(panel, new FixedRawSubInventorySlots(new[] { new SlotItem(_wood, 10) }));
            Assert.IsTrue(reader.TryRead(out var slots));
            Assert.AreEqual(10, slots[0].Count);
        }

        [Test]
        public void パネルが閉かエラー中なら生の値があっても偽()
        {
            var raw = new FixedRawSubInventorySlots(new[] { new SlotItem(_wood, 10) });
            var state = new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryPanelState.Closed);
            var reader = new BlockSlotReader(new FakePanelSource(state), raw);
            Assert.IsFalse(reader.TryRead(out var closedSlots));
            Assert.AreEqual(0, closedSlots.Count);
            state.Value = BlockInventoryTestPanels.OpenFailedChest();
            Assert.IsFalse(reader.TryRead(out var failedSlots));
            Assert.AreEqual(0, failedSlots.Count);
        }
    }
}
