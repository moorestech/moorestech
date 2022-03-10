using System.Collections.Generic;
using Core.ConfigJson;
using Core.Const;
using Core.Item;
using Core.Item.Config;
using Core.Item.Util;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Util;
using Test.Module.TestConfig;

namespace Test.CombinedTest.Server.PacketTest
{
    public class CraftInventoryPlayerInventoryMoveProtocolTest
    {
        private const int PlayerId = 1;
        private const short PacketId = 12;
        
        [Test]
        public void ItemMoveTest()
        {
            int mainSlotIndex = 2;
            int craftSlot = 0;

            //初期設定----------------------------------------------------------

            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModuleConfigPath.FolderPath);
            var _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            
            //クラフトインベントリの作成
            var craftInventory =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            //アイテムの設定
            craftInventory.InsertItem(_itemStackFactory.Create(1, 5));
            Assert.AreEqual(1, craftInventory.GetItem(craftSlot).Id);
            Assert.AreEqual(5, craftInventory.GetItem(craftSlot).Count);
            
            
            //メインのインベントリの作成
            var mainInventory =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;


            //実際にアイテムを移動するテスト--------------------------------------------------------

            //クラフトインベントリからメインインベントリへアイテムを移す
            packet.GetPacketResponse(CreateReplacePayload(1, mainSlotIndex,  craftSlot,
                5));
            //実際に移動できたか確認
            Assert.AreEqual(ItemConst.EmptyItemId, craftInventory.GetItem(craftSlot).Id);
            Assert.AreEqual(0, craftInventory.GetItem(craftSlot).Count);
            Assert.AreEqual(1, mainInventory.GetItem(mainSlotIndex).Id);
            Assert.AreEqual(5, mainInventory.GetItem(mainSlotIndex).Count);


            //メインインベントリからクラフトインベントリへアイテムを移す
            packet.GetPacketResponse(CreateReplacePayload(0,  mainSlotIndex,  craftSlot, 5));
            //きちんと移動できたか確認
            Assert.AreEqual(1, craftInventory.GetItem(craftSlot).Id);
            Assert.AreEqual(5, craftInventory.GetItem(craftSlot).Count);
            Assert.AreEqual(ItemConst.EmptyItemId, mainInventory.GetItem(mainSlotIndex).Id);
            Assert.AreEqual(0, mainInventory.GetItem(mainSlotIndex).Count);


            //別のアイテムIDが在ったとき、全て選択していれば入れ替える
            //別IDのアイテム挿入
            mainInventory.SetItem(mainSlotIndex, _itemStackFactory.Create(2, 3));
            //メインインベントリからクラフトインベントリへ全てのアイテムを移す
            packet.GetPacketResponse(CreateReplacePayload(0,  mainSlotIndex,  craftSlot, 3));
            //きちんと移動できたか確認
            Assert.AreEqual(2, craftInventory.GetItem(craftSlot).Id);
            Assert.AreEqual(3, craftInventory.GetItem(craftSlot).Count);
            Assert.AreEqual(1, mainInventory.GetItem(mainSlotIndex).Id);
            Assert.AreEqual(5, mainInventory.GetItem(mainSlotIndex).Count);


            //クラフトインベントリから一部だけ移動させようとしても移動できないテスト
            packet.GetPacketResponse(CreateReplacePayload(0,  mainSlotIndex,  craftSlot, 3));
            //移動できてないかの確認
            Assert.AreEqual(2, craftInventory.GetItem(craftSlot).Id);
            Assert.AreEqual(3, craftInventory.GetItem(craftSlot).Count);
            Assert.AreEqual(1, mainInventory.GetItem(mainSlotIndex).Id);
            Assert.AreEqual(5, mainInventory.GetItem(mainSlotIndex).Count);

            //一部だけ移動させようとしても移動できないテスト
            packet.GetPacketResponse(CreateReplacePayload(1,  mainSlotIndex,  craftSlot, 2));
            //移動できてないかの確認
            Assert.AreEqual(2, craftInventory.GetItem(craftSlot).Id);
            Assert.AreEqual(3, craftInventory.GetItem(craftSlot).Count);
            Assert.AreEqual(1, mainInventory.GetItem(mainSlotIndex).Id);
            Assert.AreEqual(5, mainInventory.GetItem(mainSlotIndex).Count);

            //同じIDならそのまま足されるテスト
            //テスト用にクラフトインベントリと同じアイテムIDを挿入
            mainInventory.SetItem(mainSlotIndex, _itemStackFactory.Create(2, 3));
            //メインからアイテム2つを移す
            packet.GetPacketResponse(CreateReplacePayload(0,  mainSlotIndex,  craftSlot, 2));

            Assert.AreEqual(2, craftInventory.GetItem(craftSlot).Id);
            Assert.AreEqual(5, craftInventory.GetItem(craftSlot).Count);
            Assert.AreEqual(2, mainInventory.GetItem(mainSlotIndex).Id);
            Assert.AreEqual(1, mainInventory.GetItem(mainSlotIndex).Count);

            //アイテムスタック数以上のアイテムを入れたときに戻されるテスト
            var max = new ItemConfig(new ConfigPath(TestModuleConfigPath.FolderPath)).GetItemConfig(2).MaxStack;
            mainInventory.SetItem(mainSlotIndex, _itemStackFactory.Create(2, max));
            //メインからアイテムを全て移す
            packet.GetPacketResponse(CreateReplacePayload(0,  mainSlotIndex,  craftSlot, max));

            Assert.AreEqual(2, craftInventory.GetItem(craftSlot).Id);
            Assert.AreEqual(max, craftInventory.GetItem(craftSlot).Count);
            Assert.AreEqual(2, mainInventory.GetItem(mainSlotIndex).Id);
            Assert.AreEqual(5, mainInventory.GetItem(mainSlotIndex).Count);
            //逆の場合
            packet.GetPacketResponse(CreateReplacePayload(1,  mainSlotIndex,  craftSlot, max));

            Assert.AreEqual(2, craftInventory.GetItem(craftSlot).Id);
            Assert.AreEqual(5, craftInventory.GetItem(craftSlot).Count);
            Assert.AreEqual(2, mainInventory.GetItem(mainSlotIndex).Id);
            Assert.AreEqual(max, mainInventory.GetItem(mainSlotIndex).Count);
        }

        private List<byte> CreateReplacePayload(short toMainFlag,int mainSlot,int craftSlot,int itemNum)
        {
            var payload = new List<byte>();
            payload.AddRange(ToByteList.Convert(PacketId));
            payload.AddRange(ToByteList.Convert(toMainFlag)); //クラフトインベントリ→メインインベントリのフラグ
            payload.AddRange(ToByteList.Convert(PlayerId));
            payload.AddRange(ToByteList.Convert(mainSlot)); //メインインベントリの移動先スロット
            payload.AddRange(ToByteList.Convert(craftSlot)); //クラフトインベントリのインデクス
            payload.AddRange(ToByteList.Convert(itemNum)); //移動するアイテム数
            return payload;
        }
    }
}