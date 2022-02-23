using System.Collections.Generic;
using System.Linq;
using Core.Const;
using Core.Item.Util;
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
            var craftingInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            for (int i = 0; i < craftConfig.Items.Count; i++)
            {
                craftingInventory.SetItem(i,craftConfig.Items[i]);
            }
            
            
            //イベントを取得
            var response = packetResponse.GetPacketResponse(EventRequest());
            Assert.AreEqual(PlayerInventoryConst.CraftingSlotSize,response.Count); //クラフトスロットのサイズ分セットしたので、そのサイズ分返ってくる
            
            
            //最後から一つ前のパケットはまだクラフト可能になっていないことを検証する
            var checkSlot = PlayerInventoryConst.CraftingSlotSize - 2;
            var enumerator = new ByteListEnumerator(response[checkSlot].ToList());
            Assert.AreEqual(3,enumerator.MoveNextToGetShort()); //パケットID
            Assert.AreEqual(4,enumerator.MoveNextToGetShort()); //イベントID
            Assert.AreEqual(checkSlot,enumerator.MoveNextToGetInt()); //インベントリスロット
            Assert.AreEqual(craftConfig.Items[checkSlot].Id,enumerator.MoveNextToGetInt()); //更新アイテムID
            Assert.AreEqual(craftConfig.Items[checkSlot].Count,enumerator.MoveNextToGetInt()); //更新アイテム数
            
            Assert.AreEqual(ItemConst.EmptyItemId,enumerator.MoveNextToGetInt()); //結果のアイテムID
            Assert.AreEqual(0,enumerator.MoveNextToGetInt()); //結果のアイテム数
            Assert.AreEqual(0,enumerator.MoveNextToGetByte()); //クラフトできないので0
            
            
            
            //最後のパケットはクラフト可能になっていることを検証する
            checkSlot = PlayerInventoryConst.CraftingSlotSize - 1;
            enumerator = new ByteListEnumerator(response[checkSlot].ToList());
            Assert.AreEqual(3,enumerator.MoveNextToGetShort()); //パケットID
            Assert.AreEqual(4,enumerator.MoveNextToGetShort()); //イベントID
            Assert.AreEqual(checkSlot,enumerator.MoveNextToGetInt()); //インベントリスロット
            Assert.AreEqual(craftConfig.Items[checkSlot].Id,enumerator.MoveNextToGetInt()); //更新アイテムID
            Assert.AreEqual(craftConfig.Items[checkSlot].Count,enumerator.MoveNextToGetInt()); //更新アイテム数
            
            Assert.AreEqual(craftConfig.Result.Id,enumerator.MoveNextToGetInt()); //結果のアイテムID
            Assert.AreEqual(craftConfig.Result.Count,enumerator.MoveNextToGetInt()); //結果のアイテム数
            Assert.AreEqual(1,enumerator.MoveNextToGetByte()); //クラフトできるので1
            
            
            
            
            //クラフト実行
            craftingInventory.Craft();
            
            
            
            
            
            //イベントが発火しているか
            response = packetResponse.GetPacketResponse(EventRequest());
            Assert.AreEqual(PlayerInventoryConst.CraftingInventorySize,response.Count); //クラフトすると全てのスロットが更新されるため、10個のイベントが発生する
            
            //最初のパケットが出力スロットであるため、その検証をする
            checkSlot = PlayerInventoryConst.CraftingInventorySize - 1;;
            enumerator = new ByteListEnumerator(response[checkSlot].ToList());
            Assert.AreEqual(3,enumerator.MoveNextToGetShort()); //パケットID
            Assert.AreEqual(4,enumerator.MoveNextToGetShort()); //イベントID
            Assert.AreEqual(checkSlot,enumerator.MoveNextToGetInt()); //インベントリスロット
            Assert.AreEqual(craftConfig.Result.Id,enumerator.MoveNextToGetInt()); //更新アイテムID
            Assert.AreEqual(craftConfig.Result.Count,enumerator.MoveNextToGetInt()); //更新アイテム数
            
            Assert.AreEqual(ItemConst.EmptyItemId,enumerator.MoveNextToGetInt()); //結果のアイテムID
            Assert.AreEqual(0,enumerator.MoveNextToGetInt()); //結果のアイテム数
            Assert.AreEqual(0,enumerator.MoveNextToGetByte()); //クラフトできないので0
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