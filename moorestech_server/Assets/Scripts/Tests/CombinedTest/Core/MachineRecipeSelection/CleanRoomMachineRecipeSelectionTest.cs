using System.Linq;
using Core.Master;
using Game.Block.Blocks.CleanRoom.Machine;
using Game.Block.Blocks.Machine;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using NUnit.Framework;
using Tests.CombinedTest.Core.CleanRoom;
using Tests.Module.TestMod;
using Tests.Util.MachineRecipe;
using UnityEngine;

namespace Tests.CombinedTest.Core.MachineRecipeSelection
{
    public class CleanRoomMachineRecipeSelectionTest
    {
        [Test]
        public void 凍結中は返却容量不足なら維持し全量返却時だけ解除する()
        {
            CleanRoomHatchTest.CreateServer();
            var block = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomMachineId, new Vector3Int(20, 0, 20));
            var processor = block.GetComponent<CleanRoomMachineProcessorComponent>();
            var input = MachineRecipeSelectionTestUtil.GetInputInventory(block);
            processor.SetCleanRoomEffect(new CleanRoomEffect(true, 4, 0));
            MachineRecipeSelectionTestUtil.SelectRecipe(block, ForUnitTestMachineRecipeId.CleanRoomMachineRecipe);
            input.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestChipRawWafer, 1));
            processor.SupplyPower(100);
            processor.Update();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

            // 加工を凍結し入力を埋め返却不能化
            // Freeze processing and fill input to block refunds
            processor.SetCleanRoomEffect(new CleanRoomEffect(false, 0, 0));
            processor.Update();
            var fillerGuid = new System.Guid("00000000-0000-0000-1234-000000000003");
            for (var i = 0; i < input.InputSlot.Count; i++) input.SetItem(i, ServerContext.ItemStackFactory.Create(fillerGuid, 1));

            var rejected = processor.TrySetRecipe(null, MachineRecipeSelectionTestUtil.CreatePlayerInventory(0));

            Assert.AreEqual(MachineRecipeChangeResult.RefundCapacityInsufficient, rejected);
            Assert.AreEqual(ProcessState.Halted, processor.CurrentState);
            Assert.AreEqual(ForUnitTestMachineRecipeId.CleanRoomMachineRecipe, processor.RecipeGuid);
            Assert.IsTrue(input.InputSlot.All(item => item.Id == MasterHolder.ItemMaster.GetItemId(fillerGuid)));

            var playerInventory = MachineRecipeSelectionTestUtil.CreatePlayerInventory(1);
            var accepted = processor.TrySetRecipe(null, playerInventory);

            Assert.AreEqual(MachineRecipeChangeResult.Success, accepted);
            Assert.IsNull(processor.RecipeGuid);
            Assert.AreEqual(ProcessState.Halted, processor.CurrentState);
            Assert.AreEqual(ForUnitTestItemId.TestChipRawWafer, playerInventory.GetItem(0).Id);
        }
    }
}
