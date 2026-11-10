using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Logic.Common;
using Core.Master;
using Game.Block.Interface.State;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Family
{
    /// <summary>
    /// 液体の列の組み立て（入力→出力の順・空の液体・パイプの節）
    /// Fluid list assembly (input-then-output order, the empty fluid, a pipe node)
    /// </summary>
    public class BlockFluidSlotConversionTest
    {
        private static readonly int EmptyId = FluidMaster.EmptyFluidId.AsPrimitive();

        [Test]
        public void 入力2から出力1の順で3件_空はEmpty_中身ありはFilled()
        {
            var inputs = new List<FluidMessagePack> { new(5, 20, 100), new(EmptyId, 0, 80) };
            var outputs = new List<FluidMessagePack> { new(7, 3, 50) };

            var slots = BlockFluidSlotConversion.FromTanks(inputs, outputs);

            Assert.AreEqual(3, slots.Count);
            var first = (FilledBlockFluidSlotState)slots[0];
            Assert.AreEqual(new FluidId(5), first.FluidId);
            Assert.AreEqual(20, first.Amount, 1e-9);
            Assert.AreEqual(100, first.Capacity, 1e-9);
            Assert.IsInstanceOf<EmptyBlockFluidSlotState>(slots[1]);
            Assert.AreEqual(80, slots[1].Capacity, 1e-9);
            var third = (FilledBlockFluidSlotState)slots[2];
            Assert.AreEqual(new FluidId(7), third.FluidId);
            Assert.AreEqual(50, third.Capacity, 1e-9);
        }

        [Test]
        public void パイプの節は空の液体ならEmpty()
        {
            var slot = BlockFluidSlotConversion.FromPipeNode(EmptyId, 0, 100);

            Assert.IsInstanceOf<EmptyBlockFluidSlotState>(slot);
            Assert.AreEqual(100, slot.Capacity, 1e-9);
        }

        [Test]
        public void パイプの節は中身ありならFilled()
        {
            var slot = (FilledBlockFluidSlotState)BlockFluidSlotConversion.FromPipeNode(3, 40, 100);

            Assert.AreEqual(new FluidId(3), slot.FluidId);
            Assert.AreEqual(40, slot.Amount, 1e-9);
        }
    }
}
