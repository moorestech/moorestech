using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.PlayerInventory.Event;
using Game.PlayerInventory.Interface.Event;
using Game.Quest.Interface;
using Game.Quest.QuestEntity;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class QuestCompletedToSendEventPacketTest
    {
        private const int PlayerId = 1;

        [Test]
        public void ItemCraftQuestCompletedToSendEventPacketTest()
        {
            var (packetResponse, serviceProvider) =
                new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var questDataStore = serviceProvider.GetService<IQuestDataStore>();

            var response = packetResponse.GetPacketResponse(EventRequestData());
            //イベントがないことを確認する
            Assert.AreEqual(0, response.Count);


            //クエストを作成、取得する
            var itemCraftQuest = (ItemCraftQuest)questDataStore.GetPlayerQuestProgress(PlayerId)[0];


            //クラフト対象のアイテムをリフレクションで取得
            var questItemId = (int)itemCraftQuest.GetType()
                .GetField("_questItemId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(itemCraftQuest);

            //TODO アイテムのクラフトが必要
            throw new NotImplementedException();

            //クエストクリアのイベントがあることを確かめる
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