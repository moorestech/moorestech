using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    // レシピ束縛によるスロット固定（ADR 0042 R5/R6）
    // Recipe-bound slot fixing (ADR 0042 R5/R6)
    public class MachineSlotBindingTest
    {
        [Test]
        public void UnselectedMachineRejectsAllInserts()
        {
            var (block, recipe, factory) = Setup(selectRecipe: false);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            var item = factory.Create(recipe.InputItems[0].ItemGuid, 3);

            var remainder = inventory.InsertItem(item);

            Assert.AreEqual(3, remainder.Count);
            Assert.IsFalse(inventory.InsertionCheck(new List<IItemStack> { item }));
            Assert.AreEqual(ItemMaster.EmptyItemId, inventory.GetItem(0).Id);
        }

        [Test]
        public void SelectedMachineRoutesEachInputToItsRecipeSlot()
        {
            var (block, recipe, factory) = Setup(selectRecipe: true);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();

            // 素材1を先に入れても素材0のスロットは空のまま
            // Inserting input 1 first leaves input 0's slot empty
            inventory.InsertItem(factory.Create(recipe.InputItems[1].ItemGuid, 1));
            inventory.InsertItem(factory.Create(recipe.InputItems[0].ItemGuid, 3));

            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(recipe.InputItems[0].ItemGuid), inventory.GetItem(0).Id);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(recipe.InputItems[1].ItemGuid), inventory.GetItem(1).Id);
        }

        [Test]
        public void SelectedMachineRejectsItemNotInRecipe()
        {
            var (block, recipe, factory) = Setup(selectRecipe: true);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            var foreign = factory.Create(recipe.OutputItems[0].ItemGuid, 1);

            var remainder = inventory.InsertItem(foreign);

            Assert.AreEqual(1, remainder.Count);
            Assert.IsFalse(inventory.InsertionCheck(new List<IItemStack> { foreign }));
        }

        [Test]
        public void ReplaceItemIntoWrongSlotIsRejected()
        {
            var (block, recipe, factory) = Setup(selectRecipe: true);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            var input1 = factory.Create(recipe.InputItems[1].ItemGuid, 1);

            // スロット0は素材0専用なので素材1は置けず、そのまま返る
            // Slot 0 is bound to input 0, so input 1 bounces back untouched
            var returned = inventory.ReplaceItem(0, input1);

            Assert.AreEqual(input1.Id, returned.Id);
            Assert.AreEqual(1, returned.Count);
            Assert.AreEqual(ItemMaster.EmptyItemId, inventory.GetItem(0).Id);
        }

        [Test]
        public void ProcessedOutputLandsInBoundOutputSlot()
        {
            var (block, recipe, factory) = Setup(selectRecipe: true);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            foreach (var input in recipe.InputItems) inventory.InsertItem(factory.Create(input.ItemGuid, input.Count));
            var processor = block.GetComponent<VanillaMachineProcessorComponent>();

            var ticks = GameUpdater.SecondsToTicks(recipe.Time) + 5;
            for (var i = 0; i < ticks; i++)
            {
                processor.SupplyExternalPower(10000);
                GameUpdater.UpdateOneTick();
            }

            // 出力スロット0（統合スロット=入力数）に生産物0、他の出力スロットは空
            // Output slot 0 (unified index = input count) holds output 0; other output slots stay empty
            var inputCount = recipe.InputItems.Length;
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid), inventory.GetItem(inputCount).Id);
            Assert.AreEqual(ItemMaster.EmptyItemId, inventory.GetItem(inputCount + 1).Id);
        }

        private static (IBlock block, Mooresmaster.Model.MachineRecipesModule.MachineRecipeMasterElement recipe, IItemStackFactory factory) Setup(bool selectRecipe)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            if (selectRecipe) MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            return (block, recipe, ServerContext.ItemStackFactory);
        }
    }
}
