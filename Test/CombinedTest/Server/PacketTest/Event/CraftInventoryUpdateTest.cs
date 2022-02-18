using System.Collections.Generic;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Util;

namespace Test.CombinedTest.Server.PacketTest.Event
{
    public class CraftInventoryUpdateTest
    {
        private const int PlayerId = 1;
        
        //クラフトした時にイベントが発火されるテスト
        [Test]
        public void CraftEventTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();

            //クラフトに必要ないアイテムを追加
            //craftingInventoryにアイテムを入れる
            var craftConfig = serviceProvider.GetService<ICraftingConfig>().GetCraftingConfigList()[0];
            var craftingInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingInventory;
            for (int i = 0; i < craftConfig.Items.Count; i++)
            {
                craftingInventory.SetItem(i,craftConfig.Items[i]);
            }
            
            
            //イベントを取得
            var response = packetResponse.GetPacketResponse(EventRequest());
            Assert.AreEqual(PlayerInventoryConst.CraftingSlotSize,response.Count); //クラフトスロットのサイズ分セットしたので、そのサイズ分返ってくる
            
            //クラフト実行
            craftingInventory.Craft();
            
            //イベントが発火しているか
            response = packetResponse.GetPacketResponse(EventRequest());
            Assert.AreEqual(PlayerInventoryConst.CraftingInventorySize,response.Count); //クラフトすると全てのスロットが更新されるため、10このイベントが発生する
        }
        
        
        
        
        List<byte> EventRequest()
        {
            var payload = new List<byte>();
            payload.AddRange(ToByteList.Convert((short) 4));
            payload.AddRange(ToByteList.Convert(PlayerId));
            return payload;
        }
    }
}