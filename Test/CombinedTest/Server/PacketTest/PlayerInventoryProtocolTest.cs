using System;
using System.Collections.Generic;
using System.Linq;
using Core.ConfigJson;
using Core.Const;
using Core.Item;
using Core.Item.Config;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Boot;
using Server.Protocol.PacketResponse;

using Server.Util;

using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest
{
    public class PlayerInventoryProtocolTest
    {
        [Test]
        public void GetPlayerInventoryProtocolTest()
        {
            int playerId = 1;

            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);


            //からの時のデータ要求
            var payload = MessagePackSerializer.Serialize(new RequestPlayerInventoryProtocolMessagePack(playerId)).ToList();
            //データの検証
            var data = MessagePackSerializer.Deserialize<PlayerInventoryResponseProtocolMessagePack>(packet.GetPacketResponse(payload)[0].ToArray());
            Assert.AreEqual(playerId, data.PlayerId);

            //プレイヤーインベントリの検証
            for (int i = 0; i < PlayerInventoryConst.MainInventoryColumns; i++)
            {
                Assert.AreEqual(ItemConst.EmptyItemId, data.Main[i].Id);
                Assert.AreEqual(0, data.Main[i].Count);
            }
            
            //グラブインベントリの検証
            Assert.AreEqual(0, data.Grab.Id);
            Assert.AreEqual(0, data.Grab.Count);
            
            //クラフトインベントリの検証
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                Assert.AreEqual(ItemConst.EmptyItemId, data.Craft[i].Id);
                Assert.AreEqual(0, data.Craft[i].Count);
            }
            //クラフト結果アイテムの検証
            Assert.AreEqual(ItemConst.EmptyItemId, data.CraftResult.Id);
            Assert.AreEqual(0, data.CraftResult.Count);
            //クラフト不可能である事の検証
            Assert.AreEqual(false, data.IsCreatable);
            
            
            
            //インベントリにアイテムが入っている時のテスト
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            playerInventoryData.MainOpenableInventory.SetItem(0, itemStackFactory.Create(1, 5));
            playerInventoryData.MainOpenableInventory.SetItem(20, itemStackFactory.Create(3, 1));
            playerInventoryData.MainOpenableInventory.SetItem(34, itemStackFactory.Create(10, 7));
            
            
            
            //クラフトに必要なアイテムの二倍の量を入れる
            var craftConfig = serviceProvider.GetService<ICraftingConfig>().GetCraftingConfigList()[0];
            for (int i = 0; i < craftConfig.CraftItemInfos.Count; i++)
            {
                var id = craftConfig.CraftItemInfos[i].ItemStack.Id;
                var count = craftConfig.CraftItemInfos[i].ItemStack.Count;
                Console.WriteLine(craftConfig.CraftItemInfos[i].ItemStack.Id);
                Console.WriteLine(craftConfig.CraftItemInfos[i].ItemStack.Count);
                playerInventoryData.CraftingOpenableInventory.SetItem(i,id,count * 2);
            };
            
            //クラフトを実行する　ここでアイテムが消費される
            playerInventoryData.CraftingOpenableInventory.NormalCraft();

            
            
            //2回目のデータ要求
            data = MessagePackSerializer.Deserialize<PlayerInventoryResponseProtocolMessagePack>(packet.GetPacketResponse(payload)[0].ToArray());
            Assert.AreEqual(playerId, data.PlayerId);

            //データの検証
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                if (i == 0)
                {
                    Assert.AreEqual(1, data.Main[i].Id);
                    Assert.AreEqual(5, data.Main[i].Count);
                }
                else if (i == 20)
                {
                    Assert.AreEqual(3, data.Main[i].Id);
                    Assert.AreEqual(1, data.Main[i].Count);
                }
                else if (i == 34)
                {
                    Assert.AreEqual(10, data.Main[i].Id);
                    Assert.AreEqual(7, data.Main[i].Count);
                }
                else
                {
                    Assert.AreEqual(ItemConst.EmptyItemId, data.Main[i].Id);
                    Assert.AreEqual(0, data.Main[i].Count);
                }
            }
            
            //グラブインベントリの検証
            //クラフトしたのでグラブインベントリに入っている
            Assert.AreEqual(craftConfig.Result.Id, data.Grab.Id);
            Assert.AreEqual(craftConfig.Result.Count, data.Grab.Count);
            
            
            //クラフトスロットの検証
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                Assert.AreEqual(craftConfig.CraftItemInfos[i].ItemStack.Id, data.Craft[i].Id);
                Assert.AreEqual(craftConfig.CraftItemInfos[i].ItemStack.Count, data.Craft[i].Count);
            }
            //まだクラフトスロットにアイテムがあるため、クラフト可能である事の検証
            Assert.AreEqual(true, data.IsCreatable);

            //クラフト結果アイテムの検証
            Assert.AreEqual(craftConfig.Result.Id, data.CraftResult.Id);
            Assert.AreEqual(craftConfig.Result.Count, data.CraftResult.Count);
        }
    }
}