using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.PlayerInventory.Interface.Event;
using Game.Quest.Interface;
using Game.Quest.QuestEntity;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PlayerInventory.Event;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest.Event
{
    public class QuestCompletedToSendEventPacketTest
    {
        private const int PlayerId = 1;
        
        [Test]
        public void ItemCraftQuestCompletedToSendEventPacketTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var questDataStore = serviceProvider.GetService<IQuestDataStore>();
            var craftingEvent = (CraftingEvent)serviceProvider.GetService<ICraftingEvent>();
            
            var response = packetResponse.GetPacketResponse(EventRequestData());
            //イベントがないことを確認する
            Assert.AreEqual(0, response.Count);
            
            
            //クエストを作成、取得する
            var itemCraftQuest = (ItemCraftQuest)questDataStore.GetPlayerQuestProgress(PlayerId)[0];
            
            
            
            //クラフト対象のアイテムをリフレクションで取得
            var questItemId = (int)itemCraftQuest.GetType().GetField("_questItemId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(itemCraftQuest);
            //リフレクションでメソッドを取得、実行
            var method = typeof(CraftingEvent).GetMethod("InvokeEvent", BindingFlags.NonPublic | BindingFlags.Instance);
            //クラフトイベントを発火することで擬似的にクラフトを再現する
            method.Invoke(craftingEvent,new object?[]{questItemId,1});
            
            
            

            //クエストクリアのイベントがあることを確かめる
            response = packetResponse.GetPacketResponse(EventRequestData());
            Assert.AreEqual(1, response.Count);
            var data = MessagePackSerializer.Deserialize<QuestCompletedEventMessagePack>(response[0].ToArray());
            Assert.AreEqual(itemCraftQuest.Quest.QuestId,data.QuestId);
        }
        
        

        List<byte> EventRequestData()
        {
            return MessagePackSerializer.Serialize(new EventProtocolMessagePack(PlayerId)).ToList();;
        }
    }
}