using System;
using System.Linq;
using Core.Inventory;
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
    public class MachineRecipeRefundTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 加工中変更は消費アイテムを機械入力へ返す()
        {
            var block = StartProcessing(ForUnitTestMachineRecipeId.MachineIdRecipe);
            var processor = block.GetComponent<VanillaMachineProcessorComponent>();

            var result = processor.TrySetRecipe(ForUnitTestMachineRecipeId.AlternativeMachineIdRecipe,
                MachineRecipeSelectionTestUtil.CreatePlayerInventory(0));

            var input = MachineRecipeSelectionTestUtil.GetInputInventory(block);
            Assert.AreEqual(MachineRecipeChangeResult.Success, result);
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            Assert.AreEqual(ForUnitTestMachineRecipeId.AlternativeMachineIdRecipe, processor.RecipeGuid);
            Assert.AreEqual(2, input.InputSlot.Count(item => 0 < item.Count));
        }

        [Test]
        public void 機械入力に入らない分をプレイヤーへ返す()
        {
            var block = StartProcessing(ForUnitTestMachineRecipeId.MachineIdRecipe);
            FillMachineInput(block);
            var playerInventory = MachineRecipeSelectionTestUtil.CreatePlayerInventory(2);

            var result = block.GetComponent<VanillaMachineProcessorComponent>()
                .TrySetRecipe(ForUnitTestMachineRecipeId.AlternativeMachineIdRecipe, playerInventory);

            Assert.AreEqual(MachineRecipeChangeResult.Success, result);
            Assert.AreEqual(2, playerInventory.InventoryItems.Count(item => 0 < item.Count));
            Assert.AreEqual(4, playerInventory.InventoryItems.Sum(item => item.Count));
        }

        [Test]
        public void 全量返却不能なら変更せず加工を継続する()
        {
            var block = StartProcessing(ForUnitTestMachineRecipeId.MachineIdRecipe);
            FillMachineInput(block);
            var playerInventory = MachineRecipeSelectionTestUtil.CreatePlayerInventory(1);
            playerInventory.SetItem(0, ServerContext.ItemStackFactory.Create(new Guid("00000000-0000-0000-1234-000000000005"), 1));
            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            var inputBefore = MachineRecipeSelectionTestUtil.GetInputInventory(block).InputSlot
                .Select(item => (item.Id, item.Count)).ToArray();

            var result = processor.TrySetRecipe(ForUnitTestMachineRecipeId.AlternativeMachineIdRecipe, playerInventory);

            Assert.AreEqual(MachineRecipeChangeResult.RefundCapacityInsufficient, result);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(ForUnitTestMachineRecipeId.MachineIdRecipe, processor.RecipeGuid);
            CollectionAssert.AreEqual(inputBefore, MachineRecipeSelectionTestUtil.GetInputInventory(block).InputSlot
                .Select(item => (item.Id, item.Count)).ToArray());
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(new Guid("00000000-0000-0000-1234-000000000005")), playerInventory.GetItem(0).Id);
            Assert.AreEqual(1, playerInventory.GetItem(0).Count);
        }

        [Test]
        public void 触媒は消費も返却もせず一つだけ残す()
        {
            var block = StartProcessing(ForUnitTestMachineRecipeId.UnlockedMachineRecipe);
            var result = block.GetComponent<VanillaMachineProcessorComponent>()
                .TrySetRecipe(null, MachineRecipeSelectionTestUtil.CreatePlayerInventory(0));

            var inputItems = MachineRecipeSelectionTestUtil.GetInputInventory(block).InputSlot.Where(item => 0 < item.Count).ToList();
            var catalystId = MasterHolder.ItemMaster.GetItemId(new Guid("00000000-0000-0000-1234-000000000004"));
            Assert.AreEqual(MachineRecipeChangeResult.Success, result);
            Assert.AreEqual(1, inputItems.Count(item => item.Id == catalystId));
            Assert.AreEqual(3, inputItems.Sum(item => item.Count));
        }

        private static IBlock StartProcessing(Guid recipeGuid)
        {
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid);
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            foreach (var input in recipe.InputItems) inventory.InsertItem(ServerContext.ItemStackFactory.Create(input.ItemGuid, input.Count));
            Assert.AreEqual(MachineRecipeChangeResult.Success,
                MachineRecipeSelectionTestUtil.SelectRecipe(block, recipeGuid));
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Processing, block.GetComponent<VanillaMachineProcessorComponent>().CurrentState);
            return block;
        }

        private static void FillMachineInput(IBlock block)
        {
            var input = MachineRecipeSelectionTestUtil.GetInputInventory(block);
            input.InsertItem(ServerContext.ItemStackFactory.Create(new Guid("00000000-0000-0000-1234-000000000003"), 1));
            input.InsertItem(ServerContext.ItemStackFactory.Create(new Guid("00000000-0000-0000-1234-000000000004"), 1));
        }
    }
}
