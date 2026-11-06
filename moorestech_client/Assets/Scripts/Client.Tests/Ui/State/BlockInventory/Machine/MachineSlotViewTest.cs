using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Core.Master;
using NUnit.Framework;
using static Client.Tests.Ui.State.BlockInventory.Machine.MachineSlotGhostFixtures;

namespace Client.Tests.Ui.State.BlockInventory.Machine
{
    /// <summary>
    /// Web の machineSlotGhosts.test.ts のうち buildMachineSlotView の写し（L0）
    /// Port of the buildMachineSlotView part of Web machineSlotGhosts.test.ts (L0)
    /// </summary>
    public class MachineSlotViewTest
    {
        /// <web-test file="src/features/blockInventory/details/machine/machineSlotGhosts.test.ts">束縛スロットだけを列挙し、対応する位置にghostを返す（R7・2026-08-30裁定）</web-test>
        [Test]
        public void 束縛スロットだけを列挙し対応する位置にゴーストを返す()
        {
            // 束縛外（input index2・output index4,5）は空なら描かない
            // Unbound slots (input index2, output index4,5) stay undrawn while empty
            var view = MachineSlotGhosts.BuildMachineSlotView(Machine(), EmptyItemSlots(), EmptyFluidSlots(3));
            Assert.AreEqual(2, view.Inputs.Count);
            AssertGhostSlot(view.Inputs[0], 0, 1, 2, new[] { 1, 11, 12 });
            AssertGhostSlot(view.Inputs[1], 1, 5, 1, new[] { 5 });
            Assert.AreEqual(1, view.Outputs.Count);
            AssertGhostSlot(view.Outputs[0], 3, 2, 1, new[] { 2 });
        }

        /// <web-test file="src/features/blockInventory/details/machine/machineSlotGhosts.test.ts">束縛外スロットに実アイテムがある場合だけそのスロットも描く（ghost無し・戻しきれなかった残置分）</web-test>
        [Test]
        public void 束縛外スロットに実アイテムが残るときだけゴースト無しで描く()
        {
            // input index2（束縛外）に返却しきれなかった旧素材が残っている
            // input index2 (unbound) holds a leftover material the refund could not return
            var itemSlots = EmptyItemSlots();
            itemSlots[2] = new BlockItemSlotState(new ItemId(9), 4);
            var view = MachineSlotGhosts.BuildMachineSlotView(Machine(), itemSlots, EmptyFluidSlots(3));
            Assert.AreEqual(3, view.Inputs.Count);
            AssertGhostSlot(view.Inputs[0], 0, 1, 2, new[] { 1, 11, 12 });
            AssertGhostSlot(view.Inputs[1], 1, 5, 1, new[] { 5 });
            AssertGhostlessSlot(view.Inputs[2], 2);
        }

        /// <web-test file="src/features/blockInventory/details/machine/machineSlotGhosts.test.ts">液体は束縛されたタンクだけを返す</web-test>
        [Test]
        public void 液体は束縛されたタンクだけを返す()
        {
            var view = MachineSlotGhosts.BuildMachineSlotView(Machine(), EmptyItemSlots(), EmptyFluidSlots(3));
            Assert.AreEqual(1, view.Fluids.Count);
            Assert.AreEqual(0, view.Fluids[0].Index);
            Assert.IsInstanceOf<GhostedFluidSlot>(view.Fluids[0]);
            Assert.AreEqual(InputFluid, ((GhostedFluidSlot)view.Fluids[0]).Ghost.FluidId);
            Assert.AreEqual(10, ((GhostedFluidSlot)view.Fluids[0]).Ghost.Amount);
        }

        /// <web-test file="src/features/blockInventory/details/machine/machineSlotGhosts.test.ts">出力液体は入力タンク数の後ろの番号を指す</web-test>
        [Test]
        public void 出力液体は入力タンク数の後ろの番号を指す()
        {
            var outputFluid = new FluidId(2);
            var machine = WithTanks(new[] { new MachineTankBinding(2, outputFluid, 4) });
            var view = MachineSlotGhosts.BuildMachineSlotView(machine, EmptyItemSlots(), EmptyFluidSlots(3));
            Assert.AreEqual(1, view.Fluids.Count);
            Assert.AreEqual(2, view.Fluids[0].Index);
        }

        /// <web-test file="src/features/blockInventory/details/machine/machineSlotGhosts.test.ts">束縛外でも中身の残っているタンクは描く（液体側の残置救済）</web-test>
        [Test]
        public void 束縛外でも中身の残っているタンクは描く()
        {
            var fluidSlots = EmptyFluidSlots(3);
            fluidSlots[1] = BlockFluidSlotState.Filled(new FluidId(3), 20, 100);
            var view = MachineSlotGhosts.BuildMachineSlotView(Machine(), EmptyItemSlots(), fluidSlots);
            Assert.AreEqual(2, view.Fluids.Count);
            Assert.AreEqual(0, view.Fluids[0].Index);
            Assert.IsInstanceOf<GhostedFluidSlot>(view.Fluids[0]);
            Assert.AreEqual(1, view.Fluids[1].Index);
            Assert.IsInstanceOf<PlainFluidSlot>(view.Fluids[1]);
        }

        /// <web-test file="src/features/blockInventory/details/machine/machineSlotGhosts.test.ts">束縛が無い機械は全実スロットをghost無しで返す（レシピ0件機械・R11現状維持）</web-test>
        [Test]
        public void 束縛が無い機械は全実スロットをゴースト無しで返す()
        {
            var view = MachineSlotGhosts.BuildMachineSlotView(Unbound(), EmptyItemSlots(), EmptyFluidSlots(2));
            Assert.AreEqual(3, view.Inputs.Count);
            for (var i = 0; i < 3; i++) AssertGhostlessSlot(view.Inputs[i], i);
            Assert.AreEqual(3, view.Outputs.Count);
            for (var i = 0; i < 3; i++) AssertGhostlessSlot(view.Outputs[i], i + 3);
            Assert.AreEqual(2, view.Fluids.Count);
            Assert.AreEqual(0, view.Fluids[0].Index);
            Assert.AreEqual(1, view.Fluids[1].Index);
            Assert.IsInstanceOf<PlainFluidSlot>(view.Fluids[0]);
            Assert.IsInstanceOf<PlainFluidSlot>(view.Fluids[1]);
        }
    }
}
