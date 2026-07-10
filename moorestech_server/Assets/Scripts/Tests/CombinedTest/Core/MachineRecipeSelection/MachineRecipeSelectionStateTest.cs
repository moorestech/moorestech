using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util.MachineRecipe;
using UnityEngine;

namespace Tests.CombinedTest.Core.MachineRecipeSelection
{
    public class MachineRecipeSelectionStateTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void Recipe未選択では材料があっても加工を開始しない()
        {
            var block = PlaceMachineWithInputs(ForUnitTestMachineRecipeId.MachineIdRecipe);
            var input = MachineRecipeSelectionTestUtil.GetInputInventory(block);

            GameUpdater.UpdateOneTick();

            Assert.AreEqual(ProcessState.Idle, block.GetComponent<VanillaMachineProcessorComponent>().CurrentState);
            Assert.AreEqual(2, input.InputSlot.Count(item => 0 < item.Count));
        }

        [Test]
        public void 選択したレシピだけで加工を開始する()
        {
            var block = PlaceMachineWithInputs(ForUnitTestMachineRecipeId.MachineIdRecipe);
            var result = MachineRecipeSelectionTestUtil.SelectRecipe(block, ForUnitTestMachineRecipeId.MachineIdRecipe);

            GameUpdater.UpdateOneTick();

            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            Assert.AreEqual(MachineRecipeChangeResult.Success, result);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(ForUnitTestMachineRecipeId.MachineIdRecipe, processor.RecipeGuid);
            Assert.IsTrue(MachineRecipeSelectionTestUtil.GetInputInventory(block).InputSlot.All(item => item.Count == 0));
        }

        [Test]
        public void 同じ材料でも選択したGUIDの出力を生成する()
        {
            var block = PlaceMachineWithInputs(ForUnitTestMachineRecipeId.AlternativeMachineIdRecipe);
            MachineRecipeSelectionTestUtil.SelectRecipe(block, ForUnitTestMachineRecipeId.AlternativeMachineIdRecipe);
            var processor = block.GetComponent<VanillaMachineProcessorComponent>();

            for (var i = 0; i < 100 && MachineRecipeSelectionTestUtil.GetOutputInventory(block).OutputSlot.All(item => item.Count == 0); i++)
            {
                processor.SupplyPower(100);
                GameUpdater.UpdateOneTick();
            }

            var outputs = MachineRecipeSelectionTestUtil.GetOutputInventory(block).OutputSlot.Where(item => 0 < item.Count).ToList();
            var expectedId = MasterHolder.ItemMaster.GetItemId(new Guid("00000000-0000-0000-1234-000000000005"));
            Assert.AreEqual(1, outputs.Count);
            Assert.AreEqual(expectedId, outputs[0].Id);
        }

        [Test]
        public void 不正または対象外または未解放レシピを拒否する()
        {
            var block = PlaceMachineWithInputs(ForUnitTestMachineRecipeId.MachineIdRecipe);
            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            var playerInventory = MachineRecipeSelectionTestUtil.CreatePlayerInventory(10);

            // 各失敗で選択不変も確認
            // Confirm each failure preserves the selection
            Assert.AreEqual(MachineRecipeChangeResult.RecipeNotFound,
                processor.TrySetRecipe(Guid.NewGuid(), playerInventory));
            Assert.AreEqual(MachineRecipeChangeResult.RecipeForDifferentBlock,
                processor.TrySetRecipe(ForUnitTestMachineRecipeId.BlockIdRecipe, playerInventory));
            Assert.AreEqual(MachineRecipeChangeResult.RecipeLocked,
                processor.TrySetRecipe(ForUnitTestMachineRecipeId.LockedMachineIdRecipe, playerInventory));
            Assert.IsNull(processor.RecipeGuid);
        }

        [Test]
        public void 同じレシピの再設定は加工を中断しない()
        {
            var block = PlaceMachineWithInputs(ForUnitTestMachineRecipeId.MachineIdRecipe);
            MachineRecipeSelectionTestUtil.SelectRecipe(block, ForUnitTestMachineRecipeId.MachineIdRecipe);
            GameUpdater.UpdateOneTick();
            var processor = block.GetComponent<VanillaMachineProcessorComponent>();

            var result = processor.TrySetRecipe(ForUnitTestMachineRecipeId.MachineIdRecipe, MachineRecipeSelectionTestUtil.CreatePlayerInventory(0));

            Assert.AreEqual(MachineRecipeChangeResult.Success, result);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(ForUnitTestMachineRecipeId.MachineIdRecipe, processor.RecipeGuid);
        }

        private static IBlock PlaceMachineWithInputs(Guid recipeGuid)
        {
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid);
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            foreach (var input in recipe.InputItems) inventory.InsertItem(ServerContext.ItemStackFactory.Create(input.ItemGuid, input.Count));
            return block;
        }
    }
}
