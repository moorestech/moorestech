
using System.Collections.Generic;
using System.Linq;
using Core.Const;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Test.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class CraftInventoryUpdateTest
    {
        private const int PlayerId = 1;

        [Test]
        public void CraftEventTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            //クラフトに必要ないアイテムを追加
            //craftingInventoryにアイテムを入れる
            var craftConfig = serviceProvider.GetService<ICraftingConfig>().GetCraftingConfigList()[0];
            var craftingInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            for (var i = 0; i < craftConfig.CraftItemInfos.Count; i++) craftingInventory.SetItem(i, craftConfig.CraftItemInfos[i].ItemStack);

            //イベントを取得
            var response = packetResponse.GetPacketResponse(EventRequest());

            const int craftEventCount = 6;

            Assert.AreEqual(craftEventCount, response.Count); //クラフトスロットのサイズ分セットしたので、そのサイズ分返ってくる


            //最後から一つ前のパケットはまだクラフト可能になっていないことを検証する
            var checkSlot = craftEventCount - 2;


            var data = MessagePackSerializer.Deserialize<CraftingInventoryUpdateEventMessagePack>(response[checkSlot].ToArray());

            Assert.AreEqual(7, data.Slot); //インベントリスロット レシピによって変わるので固定値
            Assert.AreEqual(craftConfig.CraftItemInfos[7].ItemStack.Id, data.Item.Id); //更新アイテムID
            Assert.AreEqual(craftConfig.CraftItemInfos[7].ItemStack.Count, data.Item.Count); //更新アイテム数

            Assert.AreEqual(ItemConst.EmptyItemId, data.CreatableItem.Id); //結果のアイテムID
            Assert.AreEqual(0, data.CreatableItem.Count); //結果のアイテム数
            Assert.AreEqual(false, data.IsCraftable); //クラフトできないので0


            //最後のパケットはクラフト可能になっていることを検証する
            checkSlot = craftEventCount - 1;
            data = MessagePackSerializer.Deserialize<CraftingInventoryUpdateEventMessagePack>(response[checkSlot].ToArray());
            Assert.AreEqual(8, data.Slot); //インベントリスロット レシピによって変わるので固定値
            Assert.AreEqual(craftConfig.CraftItemInfos[8].ItemStack.Id, data.Item.Id); //更新アイテムID
            Assert.AreEqual(craftConfig.CraftItemInfos[8].ItemStack.Count, data.Item.Count); //更新アイテム数

            Assert.AreEqual(craftConfig.Result.Id, data.CreatableItem.Id); //結果のアイテムID
            Assert.AreEqual(craftConfig.Result.Count, data.CreatableItem.Count); //結果のアイテム数
            Assert.AreEqual(true, data.IsCraftable); //クラフトできるので1


            //クラフト実行
            craftingInventory.NormalCraft();


            //イベントが発火しているか
            response = packetResponse.GetPacketResponse(EventRequest());
            Assert.AreEqual(craftEventCount + 1, response.Count); //クラフトすると全てのスロットが更新されるため、craftEventCount + 1個のイベントが発生する

            //つかみインベントリが更新されてるかチェック
            checkSlot = craftEventCount + 1 - 1;
            var grabData = MessagePackSerializer.Deserialize<GrabInventoryUpdateEventMessagePack>(response[checkSlot].ToArray());

            Assert.AreEqual(craftConfig.Result.Id, grabData.Item.Id); //更新アイテムID
            Assert.AreEqual(craftConfig.Result.Count, grabData.Item.Count); //更新アイテム数
        }

        //クラフトした時にイベントが発火されるテスト


        private List<byte> EventRequest()
        {
            return MessagePackSerializer.Serialize(new EventProtocolMessagePack(PlayerId)).ToList();
        }
    }
}