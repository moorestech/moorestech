#if NET6_0
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
            
            Assert.AreEqual(0, response.Count);


            
            var itemCraftQuest = (ItemCraftQuest)questDataStore.GetPlayerQuestProgress(PlayerId)[0];


            
            var questItemId = (int)itemCraftQuest.GetType().GetField("_questItemId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(itemCraftQuest);
            
            var method = typeof(CraftingEvent).GetMethod("InvokeEvent", BindingFlags.NonPublic | BindingFlags.Instance);
            
            method.Invoke(craftingEvent, new object?[] { questItemId, 1 });


            
            response = packetResponse.GetPacketResponse(EventRequestData());
            Assert.AreEqual(1, response.Count);
            var data = MessagePackSerializer.Deserialize<QuestCompletedEventMessagePack>(response[0].ToArray());
            Assert.AreEqual(itemCraftQuest.QuestConfig.QuestId, data.QuestId);
        }


        private List<byte> EventRequestData()
        {
            return MessagePackSerializer.Serialize(new EventProtocolMessagePack(PlayerId)).ToList();
            ;
        }
    }
}
#endif