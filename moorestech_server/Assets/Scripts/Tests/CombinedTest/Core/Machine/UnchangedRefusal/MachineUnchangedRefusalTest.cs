using System;
using Core.Item;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.MachineRecipesModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core.Machine
{
    // 詰まった搬入元は同じ個体を毎tick渡し直す。機械は何も変わらない間だけ答えを使い回し、変わったら必ず計算し直す
    // A blocked source re-offers the same instance every tick; the machine reuses its answer only while nothing changed and always recomputes once something did
    public class MachineUnchangedRefusalTest
    {
        private static readonly Guid BasicRecipeGuid = new("bd3d4d7d-9c3b-4ae1-875b-950327eedd9d");

        [Test]
        public void 断られ続けた同じ個体はスロットが空いた時点で入る()
        {
            var (block, recipe) = PlaceMachine();
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            var blockInventory = (IBlockInventory)inventory;
            var itemId = MasterHolder.ItemMaster.GetItemId(recipe.InputItems[0].ItemGuid);
            var cap = recipe.InputItems[0].Count * 3;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(itemId, cap));

            // 上限まで入った機械は同じ個体を何度渡しても断り、個体をそのまま返す
            // A machine filled to the cap refuses the same instance however often it is offered, returning that very instance
            var waiting = ServerContext.ItemStackFactory.Create(itemId, 5);
            for (var i = 0; i < 3; i++) Assert.AreSame(waiting, blockInventory.InsertItem(waiting, InsertItemContext.Empty));
            Assert.AreEqual(cap, inventory.GetItem(0).Count);

            // 加工で減った扱いでスロットを空けると、同じ個体が次の渡し直しで入る
            // Once the slot is freed as if processing consumed it, the same instance goes in on the next re-offer
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(itemId, cap - 3));
            var remainder = blockInventory.InsertItem(waiting, InsertItemContext.Empty);

            Assert.AreEqual(cap, inventory.GetItem(0).Count);
            Assert.AreEqual(2, remainder.Count);
        }

        [Test]
        public void レシピ未選択で断られた同じ個体はレシピを選んだ時点で入る()
        {
            var (block, recipe) = PlaceMachine();
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            var blockInventory = (IBlockInventory)inventory;
            var waiting = ServerContext.ItemStackFactory.Create(recipe.InputItems[0].ItemGuid, 2);

            for (var i = 0; i < 3; i++) Assert.AreSame(waiting, blockInventory.InsertItem(waiting, InsertItemContext.Empty));

            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            var remainder = blockInventory.InsertItem(waiting, InsertItemContext.Empty);

            Assert.AreEqual(2, inventory.GetItem(0).Count);
            Assert.AreEqual(0, remainder.Count);
        }

        [Test]
        public void スタック上限で断られた同じ個体は上限が上がった時点で入る()
        {
            var (_, recipe) = PlaceMachine();
            var itemId = MasterHolder.ItemMaster.GetItemId(recipe.InputItems[0].ItemGuid);
            var master = MasterHolder.ItemMaster.GetItemMaster(itemId);
            var counts = MasterHolder.ItemMaster.GetStackLevelTable(master.StackLevelTableGuid).StackCounts;
            var originalLimit = counts[0];
            try
            {
                // 要求数がスタック上限を超えるレシピにして、スタック上限が先に効く状態を作る（MachineAutoInsertCapTest.StackLimitWinsWhenRecipeCapIsLarger と同じ作り）
                // Use a recipe requiring more than the stack limit so the stack limit binds first (same construction as MachineAutoInsertCapTest.StackLimitWinsWhenRecipeCapIsLarger)
                var json = JObject.FromObject(recipe);
                var input = (JObject)((JArray)json.GetValue("inputItems", StringComparison.OrdinalIgnoreCase))[0];
                input.Property("count", StringComparison.OrdinalIgnoreCase).Value = originalLimit + 50;
                var largeRecipe = json.ToObject<MachineRecipeMasterElement>();
                var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
                ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one * 4, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
                MachineRecipeSelectTestUtil.SelectRecipe(block, largeRecipe);
                var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
                var blockInventory = (IBlockInventory)inventory;
                blockInventory.InsertItem(ServerContext.ItemStackFactory.Create(itemId, originalLimit), InsertItemContext.Empty);

                var waiting = ServerContext.ItemStackFactory.Create(itemId, 10);
                for (var i = 0; i < 3; i++) Assert.AreSame(waiting, blockInventory.InsertItem(waiting, InsertItemContext.Empty));

                // スロットの個体もレシピも変わらないまま上限だけが上がる。それでも答えを使い回さず入れること
                // Only the limit rises while the slot instance and recipe stay the same; the answer must still be recomputed and the item taken
                counts[0] = originalLimit + 10;
                var remainder = blockInventory.InsertItem(waiting, InsertItemContext.Empty);

                Assert.AreEqual(originalLimit + 10, inventory.GetItem(0).Count);
                Assert.AreEqual(0, remainder.Count);
            }
            finally
            {
                // 他テストへ上限変更を持ち越さない
                // Keep the stack limit change isolated from other tests
                counts[0] = originalLimit;
            }
        }

        private static (IBlock block, MachineRecipeMasterElement recipe) PlaceMachine()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(BasicRecipeGuid);
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            return (block, recipe);
        }
    }
}
