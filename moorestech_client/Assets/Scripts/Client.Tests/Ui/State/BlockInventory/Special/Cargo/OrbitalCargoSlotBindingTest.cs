using System.Linq;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using static Client.Tests.Ui.State.BlockInventory.Special.Cargo.OrbitalCargoSamples;

namespace Client.Tests.Ui.State.BlockInventory.Special.Cargo
{
    public class OrbitalCargoSlotBindingTest
    {
        // 資材 2 枠（0・1）と積荷 3 枠（2=Lv1・3=Lv2・4=ケーブル）
        // Two material slots (0, 1) and three payload slots (2=Lv1, 3=Lv2, 4=cable)
        private static readonly RequiredItemSlot[] Materials = { new(0, new ItemId(5), 20), new(1, new ItemId(6), 5) };
        private static readonly RequiredItemSlot[] Payloads = { new(2, ChipLv1, 20), new(3, ChipLv2, 20), new(4, Cable, 1000) };

        [Test]
        public void 未選択の発射台はShiftで何も受けない()
        {
            CollectionAssert.IsEmpty(OrbitalCargoSlotBinding.PadShiftBindings(Materials, Payloads, Pad(null, null)));
            CollectionAssert.IsEmpty(OrbitalCargoSlotBinding.ElevatorShiftBindings(0, null));
        }

        [Test]
        public void 選んだ発射台は資材と選んだ積荷の枠だけをShiftで受ける()
        {
            var bindings = OrbitalCargoSlotBinding.PadShiftBindings(Materials, Payloads, Pad(ChipLv2, null));
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, bindings.Select(b => b.Slot).ToArray());
            CollectionAssert.AreEqual(new[] { ChipLv2 }, bindings[2].AcceptedItemIds.ToArray());
            var elevator = OrbitalCargoSlotBinding.ElevatorShiftBindings(0, ChipLv1).Single();
            CollectionAssert.AreEqual(new[] { ChipLv1 }, elevator.AcceptedItemIds.ToArray());
        }

        [Test]
        public void 積荷の枠は選んだ枠を透かしつきで先に並べ前の種類の残りを後ろに並べる()
        {
            var slots = new[] { Empty(), Empty(), Empty(), new SlotItem(ChipLv2, 0), new SlotItem(Cable, 300) };
            var bound = OrbitalCargoSlotBinding.Payloads(Payloads, Pad(ChipLv1, null), slots);
            CollectionAssert.AreEqual(new[] { 2, 4 }, bound.Select(b => b.Index).ToArray(), "空の Lv2 の枠は出さず、ケーブルの残りは出す");
            var ghost = ((GhostedItemSlot)bound[0]).Ghost;
            Assert.AreEqual(ChipLv1, ghost.ItemId);
            Assert.AreEqual(20, ghost.Count);
            Assert.IsInstanceOf<PlainItemSlot>(bound[1]);
        }

        [Test]
        public void 資材の枠は必要な数の透かしを持ち拠点の入口は選ぶまで透かしを持たない()
        {
            var materials = OrbitalCargoSlotBinding.Materials(Materials);
            Assert.AreEqual(5, ((GhostedItemSlot)materials[1]).Ghost.Count);
            var unselected = new OrbitalCargoDetail(null, null, new[] { ChipLv1Choice }, new[] { ChipLv1 }, 240);
            Assert.IsInstanceOf<PlainItemSlot>(OrbitalCargoSlotBinding.Inlet(0, unselected).Single());
            var selected = new OrbitalCargoDetail(ChipLv1, null, new[] { ChipLv1Choice }, new[] { ChipLv1 }, 240);
            Assert.AreEqual(20, ((GhostedItemSlot)OrbitalCargoSlotBinding.Inlet(0, selected).Single()).Ghost.Count);
        }

        private static SlotItem Empty() => new(ItemMaster.EmptyItemId, 0);
    }
}
