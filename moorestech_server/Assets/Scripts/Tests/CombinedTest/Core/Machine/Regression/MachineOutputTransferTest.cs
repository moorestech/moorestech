using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Connection;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Service;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Core.Machine.Regression
{
    public class MachineOutputTransferTest
    {
        [Test]
        public void 搬送後の余りは同じ束縛枠へ戻り変化した枠だけ通知する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            ServerContext.WorldBlockDatastore.TryAddBlock(MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid), Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            var output = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(inventory);
            var transfer = (ConnectingInventoryListPriorityInsertItemService)typeof(VanillaMachineOutputInventory)
                .GetField("_connectInventoryService", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(output);
            var connector = (BlockConnectorComponent<IBlockInventory, BeltInventoryConnectionContext>)typeof(ConnectingInventoryListPriorityInsertItemService)
                .GetField("_blockConnectorComponent", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(transfer);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(1000, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest);
            var sink = chest.GetComponent<IOpenableBlockInventoryComponent>();
            ((Dictionary<IBlockInventory, ConnectedInfo>)connector.ConnectedTargets).Add(chest.GetComponent<IBlockInventory>(), new ConnectedInfo());
            var factory = ServerContext.ItemStackFactory;
            var itemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            var capacity = ServerContext.GetService<IItemStackLevelLookup>().GetMaxStack(itemId);
            for (var slot = 0; slot < sink.GetSlotSize(); slot++) sink.SetItem(slot, factory.Create(itemId, capacity));
            sink.SetItem(0, factory.Create(itemId, capacity - 5));
            output.SetItem(0, factory.Create(itemId, 20));
            var events = new List<BlockOpenableInventoryUpdateEventProperties>();
            using var subscription = ServerContext.BlockOpenableInventoryUpdateEvent.OnInventoryUpdated
                .Where(change => change.BlockInstanceId == block.BlockInstanceId).Subscribe(events.Add);

            // 実際の機械から共有搬送へ入り、部分搬出と同値更新の通知抑止を確かめる
            // Enter shared transfer from a real machine and check partial output and unchanged-update suppression
            output.InsertConnectInventory();
            Assert.AreEqual(15, output.OutputSlot[0].Count);
            Assert.AreEqual(capacity, sink.GetItem(0).Count);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(recipe.InputItems.Length, events[0].Slot);
            Assert.AreEqual(itemId, events[0].ItemStack.Id);
            Assert.AreEqual(15, events[0].ItemStack.Count);
            Assert.AreEqual(MachineSlotPlacementCheck.Allowed, output.CheckPlacement(0, factory.Create(itemId, 1)));
            Assert.AreEqual(MachineSlotPlacementCheck.SlotBeyondRecipeOutputs, output.CheckPlacement(recipe.OutputItems.Length, factory.Create(itemId, 1)));

            output.InsertConnectInventory();
            Assert.AreEqual(15, output.OutputSlot[0].Count);
            Assert.AreEqual(1, events.Count);
            sink.SetItem(0, factory.CreatEmpty());
            output.InsertConnectInventory();
            Assert.AreEqual(0, output.OutputSlot[0].Count);
            Assert.AreEqual(15, sink.GetItem(0).Count);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(recipe.InputItems.Length, events[1].Slot);
            Assert.AreEqual(0, events[1].ItemStack.Count);
        }
    }
}
