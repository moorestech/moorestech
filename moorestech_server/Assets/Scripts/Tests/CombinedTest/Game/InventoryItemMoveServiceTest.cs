using System;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class InventoryItemMoveServiceTest
    {
        [Test]
        public void MoveTest()
        {
            var playerId = 1;
            
            //初期設定----------------------------------------------------------
            
            var (_, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            //プレイヤーのインベントリの設定
            var playerInventoryData =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);
            
            
            //アイテムの設定
            var inventory = playerInventoryData.MainOpenableInventory;
            inventory.SetItem(0, itemStackFactory.Create(new ItemId(1), 5));
            inventory.SetItem(1, itemStackFactory.Create(new ItemId(1), 1));
            inventory.SetItem(2, itemStackFactory.Create(new ItemId(2), 1));
            
            
            //実際に移動させてテスト
            //全てのアイテムを移動させるテスト
            InventoryItemMoveService.Move(inventory,
                0, inventory, 3, 5);
            Assert.AreEqual(inventory.GetItem(0), itemStackFactory.CreatEmpty());
            Assert.AreEqual(inventory.GetItem(3), itemStackFactory.Create(new ItemId(1), 5));
            
            //一部のアイテムを移動させるテスト
            InventoryItemMoveService.Move(inventory,
                3, inventory, 0, 3);
            Assert.AreEqual(inventory.GetItem(0), itemStackFactory.Create(new ItemId(1), 3));
            Assert.AreEqual(inventory.GetItem(3), itemStackFactory.Create(new ItemId(1), 2));
            
            //一部のアイテムを移動しようとするが他にスロットがあるため失敗するテスト
            InventoryItemMoveService.Move(inventory,
                0, inventory, 2, 1);
            Assert.AreEqual(inventory.GetItem(0), itemStackFactory.Create(new ItemId(1), 3));
            Assert.AreEqual(inventory.GetItem(2), itemStackFactory.Create(new ItemId(2), 1));
            
            //全てのアイテムを移動させるテスト
            InventoryItemMoveService.Move(inventory,
                0, inventory, 2, 3);
            Assert.AreEqual(inventory.GetItem(0), itemStackFactory.Create(new ItemId(2), 1));
            Assert.AreEqual(inventory.GetItem(2), itemStackFactory.Create(new ItemId(1), 3));
            
            //アイテムを加算するテスト
            InventoryItemMoveService.Move(inventory,
                2, inventory, 1, 3);
            Assert.AreEqual(inventory.GetItem(2), itemStackFactory.CreatEmpty());
            Assert.AreEqual(inventory.GetItem(1), itemStackFactory.Create(new ItemId(1), 4));
            
            
            //全てのアイテムを同じスロットにアイテムを移動させるテスト
            InventoryItemMoveService.Move(inventory,
                1, inventory, 1, 4);
            Assert.AreEqual(inventory.GetItem(1), itemStackFactory.Create(new ItemId(1), 4));
        }

        // D1回帰(C1): 機械の束縛スロットへ束縛外アイテムを全量swapしても、両側とも書き込まれず複製・消失しないことを検証する
        // D1 regression (C1): a full-stack swap into a machine's bound slot with an unbound item writes neither side, so nothing duplicates or vanishes
        [Test]
        public void MoveTest_RejectsFullSwapAgainstBoundMachineSlot()
        {
            var playerId = 1;
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.InputItems.Length > 0);
            var boundItemId = MasterHolder.ItemMaster.GetItemId(recipe.InputItems[0].ItemGuid);
            var unboundItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            Assert.AreNotEqual(boundItemId, unboundItemId, "テスト前提: 入力素材と出力生産物は別アイテムであること");

            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();

            // 機械の入力スロット0(統合スロット順の先頭)へ束縛済みアイテムを投入
            // Insert the bound item into the machine's input slot 0 (first in the unified slot order)
            var boundCount = recipe.InputItems[0].Count;
            blockInventory.InsertItem(itemStackFactory.Create(boundItemId, boundCount));
            Assert.AreEqual(boundItemId, blockInventory.GetItem(0).Id);

            // プレイヤーは束縛外アイテムを同数保持
            // The player holds the same count of an unbound item
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);
            var playerInventory = playerInventoryData.MainOpenableInventory;
            playerInventory.SetItem(0, itemStackFactory.Create(unboundItemId, boundCount));

            // 全量swapを試みる。束縛外なので両側とも変化しないはず
            // Attempt a full-stack swap; being unbound, neither side should change
            InventoryItemMoveService.Move(playerInventory, 0, blockInventory, 0, boundCount);

            Assert.AreEqual(boundItemId, blockInventory.GetItem(0).Id, "束縛外swapで機械側の中身が消失/変化してはならない");
            Assert.AreEqual(boundCount, blockInventory.GetItem(0).Count);
            Assert.AreEqual(unboundItemId, playerInventory.GetItem(0).Id, "束縛外swapでプレイヤー側のアイテムが複製/消失してはならない");
            Assert.AreEqual(boundCount, playerInventory.GetItem(0).Count);
        }
    }
}