using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.UnlockState;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util.MachineRecipe;
using UnityEngine;

namespace Tests.CombinedTest.Core.MachineRecipeSelection
{
    public class MachineRecipeFluidCancelTest
    {
        [Test]
        public void 加工中変更では消費液体を返却せず消失させる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestMachineRecipeId.LockedMachineRecipe);
            ServerContext.GetService<IGameUnlockStateDataController>().UnlockMachineRecipe(recipe.MachineRecipeGuid);
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var input = MachineRecipeSelectionTestUtil.GetInputInventory(block);

            // レシピの品目・液体を機械入力へ設定
            // Populate machine input with recipe items and fluids
            foreach (var item in recipe.InputItems) input.InsertItem(ServerContext.ItemStackFactory.Create(item.ItemGuid, item.Count));
            for (var i = 0; i < recipe.InputFluids.Length; i++)
            {
                input.FluidInputSlot[i].FluidId = MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[i].FluidGuid);
                input.FluidInputSlot[i].Amount = recipe.InputFluids[i].Amount;
            }

            Assert.AreEqual(MachineRecipeChangeResult.Success,
                MachineRecipeSelectionTestUtil.SelectRecipe(block, recipe.MachineRecipeGuid));
            GameUpdater.UpdateOneTick();
            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.IsTrue(input.FluidInputSlot.All(container => container.Amount == 0));

            var result = processor.TrySetRecipe(null, MachineRecipeSelectionTestUtil.CreatePlayerInventory(10));

            Assert.AreEqual(MachineRecipeChangeResult.Success, result);
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            Assert.IsTrue(input.FluidInputSlot.All(container => container.Amount == 0));
            Assert.AreEqual(1, input.InputSlot.Sum(item => item.Count));
        }
    }
}
