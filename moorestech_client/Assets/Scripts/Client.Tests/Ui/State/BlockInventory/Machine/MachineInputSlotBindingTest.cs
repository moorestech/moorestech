using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Core.Master;
using NUnit.Framework;
using static Client.Tests.Ui.State.BlockInventory.Machine.MachineSlotGhostFixtures;

namespace Client.Tests.Ui.State.BlockInventory.Machine
{
    /// <summary>
    /// Web の machineSlotGhosts.test.ts のうち boundMachineInputSlotsForItem と machineInputRequirement の写し（L0）
    /// Port of the boundMachineInputSlotsForItem and machineInputRequirement parts of Web machineSlotGhosts.test.ts (L0)
    /// </summary>
    public class MachineInputSlotBindingTest
    {
        private static readonly MachineItemGhost Ghost = new MachineItemGhost(new ItemId(2), 3, Ids(new[] { 2, 12 }));

        /// <web-test file="src/features/blockInventory/details/machine/machineSlotGhosts.test.ts">素材のitemIdが束縛される入力スロットindexを返す</web-test>
        [Test]
        public void 素材のアイテムが束縛される入力スロットを返す()
        {
            CollectionAssert.AreEqual(new[] { 0 }, MachineSlotGhosts.BoundMachineInputSlotsForItem(BoundMachine(), 7, new ItemId(1)));
            CollectionAssert.AreEqual(new[] { 1 }, MachineSlotGhosts.BoundMachineInputSlotsForItem(BoundMachine(), 7, new ItemId(5)));
        }

        /// <web-test file="src/features/blockInventory/details/machine/machineSlotGhosts.test.ts">出力帯の束縛は入力帯の候補に混ざらない</web-test>
        [Test]
        public void 出力帯の束縛は入力帯の候補に混ざらない()
        {
            CollectionAssert.IsEmpty(MachineSlotGhosts.BoundMachineInputSlotsForItem(BoundMachine(), 7, new ItemId(2)));
        }

        /// <web-test file="src/features/blockInventory/details/machine/machineSlotGhosts.test.ts">束縛されないitemIdは空配列を返す</web-test>
        [Test]
        public void 束縛されないアイテムは空を返す()
        {
            CollectionAssert.IsEmpty(MachineSlotGhosts.BoundMachineInputSlotsForItem(BoundMachine(), 7, new ItemId(999)));
        }

        /// <web-test file="src/features/blockInventory/details/machine/machineSlotGhosts.test.ts">継承元スロットは上位レベルの変種でも宛先になる</web-test>
        [Test]
        public void 継承元スロットは上位レベルの変種でも宛先になる()
        {
            CollectionAssert.AreEqual(new[] { 0 }, MachineSlotGhosts.BoundMachineInputSlotsForItem(BoundMachine(), 7, new ItemId(12)));
        }

        /// <web-test file="src/features/blockInventory/details/machine/machineSlotGhosts.test.ts">実物入りの束縛スロットは入っている数/必要数を返し、足りなければlacking</web-test>
        [Test]
        public void 実物入りの束縛スロットは所持と必要を返し足りなければ不足()
        {
            AssertRequirement(new BlockItemSlotState(new ItemId(2), 1), 1, 3, true);
            AssertRequirement(new BlockItemSlotState(new ItemId(2), 3), 3, 3, false);
        }

        /// <web-test file="src/features/blockInventory/details/machine/machineSlotGhosts.test.ts">受け入れ外のアイテムが残っているスロットには出さず、上位変種には出す</web-test>
        [Test]
        public void 受け入れ外の残置品には出さず上位変種には出す()
        {
            Assert.IsFalse(MachineSlotGhosts.TryGetInputRequirement(new BlockItemSlotState(new ItemId(9), 5), BoundItemSlot.WithGhost(0, Ghost), out _));
            AssertRequirement(new BlockItemSlotState(new ItemId(12), 5), 5, 3, false);
        }

        /// <web-test file="src/features/blockInventory/details/machine/machineSlotGhosts.test.ts">空スロットと束縛外スロットには出さない</web-test>
        [Test]
        public void 空スロットと束縛外スロットには出さない()
        {
            Assert.IsFalse(MachineSlotGhosts.TryGetInputRequirement(new BlockItemSlotState(new ItemId(0), 0), BoundItemSlot.WithGhost(0, Ghost), out _));
            Assert.IsFalse(MachineSlotGhosts.TryGetInputRequirement(new BlockItemSlotState(new ItemId(2), 5), BoundItemSlot.WithoutGhost(0), out _));
        }

        private static void AssertRequirement(BlockItemSlotState slot, int owned, int required, bool lacking)
        {
            Assert.IsTrue(MachineSlotGhosts.TryGetInputRequirement(slot, BoundItemSlot.WithGhost(0, Ghost), out var requirement));
            Assert.AreEqual(owned, requirement.OwnedCount);
            Assert.AreEqual(required, requirement.RequiredCount);
            Assert.AreEqual(lacking, requirement.Lacking);
        }
    }
}
