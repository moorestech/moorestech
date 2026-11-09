using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Drag;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Inventory.Drag
{
    /// <summary>
    /// 分割ドラッグの等分がホストの SplitDragActionHandler と同じ式になることを確かめる
    /// Verifies the split-drag share uses the same formula as the host's SplitDragActionHandler
    /// </summary>
    public class SplitDragDistributionTest
    {
        private static readonly ItemId Wood = new(1);
        private static readonly ItemId Stone = new(2);

        [Test]
        public void 空と同じアイテムの行先だけに等分し端数はgrabに残す()
        {
            var operations = new RecordingPlayerInventoryOperations(9, 2);
            operations.SetGrab(new SlotItem(Wood, 10));
            operations.SetMain(4, new SlotItem(Wood, 2));
            operations.SetMain(5, new SlotItem(Stone, 1));

            var share = SplitDragDistribution.Plan(operations.ReadSnapshot(), new[] { PlayerSlotRef.Main(3), PlayerSlotRef.Main(4), PlayerSlotRef.Main(5), PlayerSlotRef.Main(3) });

            // 別アイテムの 5 と重複の 3 を除いた 2 か所に 10 ÷ 2 = 5
            // Excluding the other item at 5 and the duplicate 3 leaves 2 destinations, 10 / 2 = 5 each
            Assert.AreEqual(new[] { PlayerSlotRef.Main(3), PlayerSlotRef.Main(4) }, share.Destinations);
            Assert.AreEqual(5, share.CountPerSlot);
            Assert.AreEqual(0, share.IgnoredStaleCount);
        }

        [Test]
        public void 受けられる行先が無ければ行先は空()
        {
            var operations = new RecordingPlayerInventoryOperations(9, 2);
            operations.SetGrab(new SlotItem(Wood, 10));
            operations.SetMain(0, new SlotItem(Stone, 1));

            var share = SplitDragDistribution.Plan(operations.ReadSnapshot(), new[] { PlayerSlotRef.Main(0) });

            Assert.IsEmpty(share.Destinations);
        }

        [Test]
        public void 行先がgrabより多いと一か所あたり0個()
        {
            var operations = new RecordingPlayerInventoryOperations(9, 2);
            operations.SetGrab(new SlotItem(Wood, 2));

            var share = SplitDragDistribution.Plan(operations.ReadSnapshot(), new[] { PlayerSlotRef.Main(0), PlayerSlotRef.Main(1), PlayerSlotRef.Main(2) });

            Assert.AreEqual(0, share.CountPerSlot);
        }

        [Test]
        public void 範囲外の番地は数えて除く()
        {
            var operations = new RecordingPlayerInventoryOperations(3, 2);
            operations.SetGrab(new SlotItem(Wood, 4));

            var share = SplitDragDistribution.Plan(operations.ReadSnapshot(), new[] { PlayerSlotRef.Main(0), PlayerSlotRef.Main(8) });

            Assert.AreEqual(new[] { PlayerSlotRef.Main(0) }, share.Destinations);
            Assert.AreEqual(1, share.IgnoredStaleCount);
        }
    }
}
