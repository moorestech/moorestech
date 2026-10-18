using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.RecipeSelection;
using Tests.CombinedTest.Core.Machine.Recipe;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core.Machine
{
    // ブロックからの自動搬入はレシピ3回分で止まり、手動投入はスタック上限まで入る
    // Automated inflow from blocks stops at three crafts' worth; manual input still fills to the stack limit
    public class MachineAutoInsertCapTest
    {
        private static readonly Guid BasicRecipeGuid = new("bd3d4d7d-9c3b-4ae1-875b-950327eedd9d");
        private static readonly Guid RemainRecipeGuid = new("f79c7ee0-1ebe-4fcc-b1ec-5bcb296d9c1a");

        [Test]
        public void BlockInsertStopsAtThreeCrafts()
        {
            var (inventory, recipe) = Setup(BasicRecipeGuid);
            var blockInventory = (IBlockInventory)inventory;
            var input0 = ServerContext.ItemStackFactory.Create(recipe.InputItems[0].ItemGuid, 100);

            var remainder = blockInventory.InsertItem(input0, InsertItemContext.Empty);

            // 要求3×3=9個で止まり、残りは返る
            // Stops at 3x3=9 and the rest comes back
            Assert.AreEqual(9, inventory.GetItem(0).Count);
            Assert.AreEqual(91, remainder.Count);
            Assert.IsFalse(blockInventory.InsertionCheck(new List<IItemStack> { ServerContext.ItemStackFactory.Create(recipe.InputItems[0].ItemGuid, 1) }));
        }

        [Test]
        public void BlockInsertStopsAtRequirementForRemainInput()
        {
            var (inventory, recipe) = Setup(RemainRecipeGuid);
            var blockInventory = (IBlockInventory)inventory;

            blockInventory.InsertItem(ServerContext.ItemStackFactory.Create(recipe.InputItems[0].ItemGuid, 100), InsertItemContext.Empty);
            blockInventory.InsertItem(ServerContext.ItemStackFactory.Create(recipe.InputItems[1].ItemGuid, 100), InsertItemContext.Empty);

            // 消費素材は2×3=6個、消費しない素材は要求数1個で止まる
            // The consumed input stops at 2x3=6, the non-consumed one at its requirement of 1
            Assert.AreEqual(6, inventory.GetItem(0).Count);
            Assert.AreEqual(1, inventory.GetItem(1).Count);
        }

        [Test]
        public void ManualInsertFillsToStackLimit()
        {
            var (inventory, recipe) = Setup(BasicRecipeGuid);
            var input0 = ServerContext.ItemStackFactory.Create(recipe.InputItems[0].ItemGuid, 100);
            var stackLimit = ItemStackLevelDataStore.Instance.GetMaxStack(input0.Id);

            // プレイヤー一括移動（InsertSlot）と同じ経路で入れる
            // Insert through the same path as the player's bulk move (InsertSlot)
            var player = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 1);
            player.SetItem(0, input0);
            Assert.IsTrue(inventory.InsertionCheck(new List<IItemStack> { ServerContext.ItemStackFactory.Create(input0.Id, 10) }));
            InventoryItemInsertService.Insert(player, 0, inventory, 100);

            Assert.AreEqual(Math.Min(100, stackLimit), inventory.GetItem(0).Count);
        }

        [Test]
        public void BlockInsertDoesNotReduceSlotAlreadyOverCap()
        {
            var (inventory, recipe) = Setup(BasicRecipeGuid);
            var input0Id = MasterHolder.ItemMaster.GetItemId(recipe.InputItems[0].ItemGuid);
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(input0Id, 50));

            var remainder = ((IBlockInventory)inventory).InsertItem(ServerContext.ItemStackFactory.Create(input0Id, 5), InsertItemContext.Empty);

            // 旧セーブ等で上限超のスロットは減らさず、追加も受けない
            // A slot already over the cap (e.g. an old save) is neither reduced nor topped up
            Assert.AreEqual(50, inventory.GetItem(0).Count);
            Assert.AreEqual(5, remainder.Count);
        }

        [Test]
        public void BlockCheckUsesAccumulatedBatchWithoutWriting()
        {
            var (inventory, recipe) = Setup(BasicRecipeGuid);
            var itemId = MasterHolder.ItemMaster.GetItemId(recipe.InputItems[0].ItemGuid);
            var batch = new List<IItemStack> { ServerContext.ItemStackFactory.Create(itemId, 5), ServerContext.ItemStackFactory.Create(itemId, 5) };
            Assert.IsFalse(((IBlockInventory)inventory).InsertionCheck(batch));
            Assert.IsTrue(((IOpenableInventory)inventory).InsertionCheck(batch));
            Assert.AreEqual(0, inventory.GetItem(0).Count);
            batch[1] = ServerContext.ItemStackFactory.Create(itemId, 4);
            Assert.IsTrue(((IBlockInventory)inventory).InsertionCheck(batch));
        }

        [Test]
        public void ManualListAndItemIdEntriesBypassAutoCap()
        {
            var (inventory, recipe) = Setup(BasicRecipeGuid);
            var itemId = MasterHolder.ItemMaster.GetItemId(recipe.InputItems[0].ItemGuid);
            inventory.InsertItem(itemId, 10);
            Assert.AreEqual(10, inventory.GetItem(0).Count);
            inventory.InsertItem(new List<IItemStack> { ServerContext.ItemStackFactory.Create(itemId, 10) });
            Assert.AreEqual(20, inventory.GetItem(0).Count);
            inventory.InsertItem(ServerContext.ItemStackFactory.Create(itemId, 10));
            Assert.AreEqual(30, inventory.GetItem(0).Count);
        }

        [Test]
        public void StackLimitWinsWhenRecipeCapIsLarger()
        {
            var (inventory, recipe) = Setup(BasicRecipeGuid);
            var itemId = MasterHolder.ItemMaster.GetItemId(recipe.InputItems[0].ItemGuid);
            var stackLimit = ItemStackLevelDataStore.Instance.GetMaxStack(itemId);
            // 通常レシピを複製し、要求数をスタック上限より大きくする
            // Clone a normal recipe and make its requirement exceed the stack limit
            var json = JObject.FromObject(recipe);
            var items = (JArray)json.GetValue("inputItems", StringComparison.OrdinalIgnoreCase);
            var input = (JObject)items[0];
            var countProperty = input.Property("count", StringComparison.OrdinalIgnoreCase);
            countProperty.Value = stackLimit + 1;
            var largeRecipe = json.ToObject<MachineRecipeMasterElement>();
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one * 2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            MachineRecipeSelectTestUtil.SelectRecipe(block, largeRecipe);
            inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            ((IBlockInventory)inventory).InsertItem(ServerContext.ItemStackFactory.Create(itemId, stackLimit), InsertItemContext.Empty);
            var remainder = ((IBlockInventory)inventory).InsertItem(ServerContext.ItemStackFactory.Create(itemId, 10), InsertItemContext.Empty);
            Assert.AreEqual(stackLimit, inventory.GetItem(0).Count);
            Assert.AreEqual(10, remainder.Count);
        }

        [Test]
        public void RecipeRefundCanExceedAutoCapWithoutOverflow()
        {
            var (inventory, recipe) = Setup(BasicRecipeGuid);
            for (var i = 0; i < recipe.InputItems.Length; i++)
            {
                inventory.SetItem(i, ServerContext.ItemStackFactory.Create(recipe.InputItems[i].ItemGuid, recipe.InputItems[i].Count * 3));
            }
            // 自動搬入が止まる状態でも、取消返却は入力へ戻せる
            // Cancellation refunds still fit into input when automated inflow is capped
            var input = MachineRecipeChangeRefundTestHelper.GetInputInventory(inventory);
            var overflow = MachineRecipeChangeRefundTestHelper.CreateOverflow(0);
            var refunds = MachineRecipeRefundUtil.CreateRefundStacks(recipe);
            Assert.IsTrue(MachineRecipeRefundUtil.CanRefundAllItems(input, overflow, refunds));
            MachineRecipeRefundUtil.ExecuteRefund(input, overflow, refunds, recipe);
            Assert.AreEqual(12, inventory.GetItem(0).Count);
            Assert.AreEqual(4, inventory.GetItem(1).Count);
        }

        private static (VanillaMachineBlockInventoryComponent inventory, MachineRecipeMasterElement recipe) Setup(Guid machineRecipeGuid)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(machineRecipeGuid);
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            return (block.GetComponent<VanillaMachineBlockInventoryComponent>(), recipe);
        }
    }
}
