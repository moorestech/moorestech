using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using System;
using Server.Event.EventReceive.UnifiedInventoryEvent;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    /// <summary>
    ///     ブロックのインベントリが更新された時、イベントのパケットが更新されているかをテストする
    /// </summary>
    public class BlockInventoryUpdateEventPacketTest
    {
        private const int PlayerId = 3;
        private const short PacketId = 16;

        // テスト用機械のスロット構成（blocks.jsonのTestElectricMachineに対応）
        // Slot layout of the test machine (matches TestElectricMachine in blocks.json)
        private const int InputSlotNum = 2;
        private const int OutputSlotNum = 3;
        
        //正しくインベントリの情報が更新されたことを通知するパケットが送られるかチェックする
        [Test]
        public void BlockInventoryUpdatePacketTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            
            var worldBlockDataStore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            Vector3Int pos = new(5, 7);
            
            //ブロックをセットアップ
            worldBlockDataStore.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var blockInventory = block.GetComponent<IBlockInventory>();

            // 束縛(ADR 0042)のためレシピを選択し、対象アイテムをレシピ自身から取る
            // Binding (ADR 0042) requires a selected recipe; the target item comes from the recipe itself
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            var input1Id = MasterHolder.ItemMaster.GetItemId(recipe.InputItems[1].ItemGuid);
            var output0Id = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            sink.TakeAll();


            //インベントリを開く
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(new Vector3Int(5, 7), true), new PacketResponseContext(null));
            //ブロックにアイテムを入れる（スロット1は素材1に束縛される）
            //Add item to the block (slot 1 is bound to input 1)
            blockInventory.SetItem(1, itemStackFactory.Create(input1Id, 8));


            //イベントパケットを取得してチェック
            //Take the captured event packets and verify them
            var events = sink.TakeAll();
            Assert.AreEqual(1, events.Count);
            var payLoad = events[0].Payload;
            var data = MessagePackSerializer.Deserialize<UnifiedInventoryEventMessagePack>(payLoad);

            Assert.AreEqual(InventoryEventType.Update, data.EventType); // event type
            Assert.AreEqual(InventoryType.Block, data.Identifier.InventoryType); // inventory type
            Assert.AreEqual(1, data.Slot); // slot id
            Assert.AreEqual(input1Id.AsPrimitive(), data.Item.Id.AsPrimitive()); // item id
            Assert.AreEqual(8, data.Item.Count); // item count
            Assert.AreEqual(5, data.Identifier.BlockPosition.X); // x
            Assert.AreEqual(7, data.Identifier.BlockPosition.Y); // y


            //ブロックのインベントリを閉じる
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(new Vector3Int(5, 7), false), new PacketResponseContext(null));

            //ブロックにアイテムを入れる（スロット2は生産物0に束縛される）
            //Add item to the block (slot 2 is bound to output 0)
            blockInventory.SetItem(2, itemStackFactory.Create(output0Id, 8));


            //パケットが送られていないことをチェック
            //イベントパケットを取得
            Assert.AreEqual(0, sink.TakeAll().Count);
        }
        
        
        // 複数のインベントリを同時にサブスクライブできることをテストする
        // Test that multiple inventories can be subscribed simultaneously
        [Test]
        public void MultipleInventoriesCanBeOpenedTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            var worldBlockDataStore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;

            // ブロック1をセットアップ
            // Setup block 1
            worldBlockDataStore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(5, 7), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block1);

            // ブロック2をセットアップ
            // Setup block 2
            worldBlockDataStore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(10, 20), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block2);

            // 束縛(ADR 0042)のためブロック1のレシピを選択し、対象アイテムをレシピ自身から取る
            // Binding (ADR 0042) requires a selected recipe on block 1; the target item comes from the recipe itself
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            MachineRecipeSelectTestUtil.SelectRecipe(block1, recipe);
            var output0Id = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            sink.TakeAll();


            // 一つ目のブロックインベントリを開く
            // Open first block inventory
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(new Vector3Int(5, 7), true), new PacketResponseContext(null));
            // 二つ目のブロックインベントリを開く
            // Open second block inventory
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(new Vector3Int(10, 20), true), new PacketResponseContext(null));


            // 一つ目のブロックインベントリにアイテムを入れる（スロット2は生産物0に束縛される）
            // Add item to first block inventory (slot 2 is bound to output 0)
            var block1Inventory = block1.GetComponent<VanillaMachineBlockInventoryComponent>();
            block1Inventory.SetItem(2, itemStackFactory.Create(output0Id, 8));


            // パケットが送られていることをチェック（複数サブスクリプション対応のため）
            // Check that packet is sent (multiple subscriptions are now supported)
            var events = sink.TakeAll();
            Assert.AreEqual(1, events.Count);

            // イベントの内容を検証
            // Verify event content
            var payLoad = events[0].Payload;
            var data = MessagePackSerializer.Deserialize<UnifiedInventoryEventMessagePack>(payLoad);
            Assert.AreEqual(InventoryEventType.Update, data.EventType);
            Assert.AreEqual(InventoryType.Block, data.Identifier.InventoryType);
            Assert.AreEqual(2, data.Slot);
            Assert.AreEqual(5, data.Identifier.BlockPosition.X);
            Assert.AreEqual(7, data.Identifier.BlockPosition.Y);


            // 二つ目のブロックインベントリにアイテムを入れる（モジュールスロットは束縛が無いため未選択でも任意のIDを置ける）
            // Add item to second block inventory (module slots are unbound, so any id can be placed even unselected)
            var block2Inventory = block2.GetComponent<VanillaMachineBlockInventoryComponent>();
            const int moduleRangeStart = InputSlotNum + OutputSlotNum;
            block2Inventory.SetItem(moduleRangeStart, itemStackFactory.Create(new ItemId(5), 10));


            // パケットが送られていることをチェック
            // Check that packet is sent
            events = sink.TakeAll();
            Assert.AreEqual(1, events.Count);

            // イベントの内容を検証
            // Verify event content
            payLoad = events[0].Payload;
            data = MessagePackSerializer.Deserialize<UnifiedInventoryEventMessagePack>(payLoad);
            Assert.AreEqual(InventoryEventType.Update, data.EventType);
            Assert.AreEqual(InventoryType.Block, data.Identifier.InventoryType);
            Assert.AreEqual(moduleRangeStart, data.Slot);
            Assert.AreEqual(10, data.Identifier.BlockPosition.X);
            Assert.AreEqual(20, data.Identifier.BlockPosition.Y);
        }
        
        
        private byte[] OpenCloseBlockInventoryPacket(Vector3Int pos, bool isOpen)
        {
            var identifier = InventoryIdentifierMessagePack.CreateBlockMessage(pos);
            return MessagePackSerializer
                .Serialize(new SubscribeInventoryProtocol.SubscribeInventoryRequestMessagePack(PlayerId, identifier, isOpen));
        }
    }
}
