using System.Linq;
using Game.Save.Interface;
using Game.Save.Json;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest
{
    public class QuestProgressRequestProtocolTest
    {

        private const int PlayerId = 1;
        /// <summary>
        /// 現在のクエスト進捗状況を取得するテスト
        /// </summary>
        [Test]
        public void GetTest()
        {
            //テスト用のセーブデータ
            var json = "{\"world\":[],\"playerInventory\":[],\"entities\":[],\"quests\":{\"1\":[{\"id\":\"QuestAuthor:forQuestTest:Test1\",\"co\":false,\"re\":false},{\"id\":\"QuestAuthor:forQuestTest:Test2\",\"co\":true,\"re\":false},{\"id\":\"QuestAuthor:forQuestTest:Test3\",\"co\":true,\"re\":true},{\"id\":\"QuestAuthor:forQuestTest:Test4\",\"co\":false,\"re\":false}]}}";

            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            (serviceProvider.GetService<ILoadRepository>() as LoadJsonFile).Load(json);


            //クエストのデータ要求クラス
            var payload = MessagePackSerializer.Serialize(new QuestProgressRequestProtocolMessagePack(PlayerId)).ToList();
            //データの検証
            var questResponse = MessagePackSerializer.Deserialize<QuestProgressResponseProtocolMessagePack>(packet.GetPacketResponse(payload)[0].ToArray()).Quests;
            
            Assert.AreEqual("QuestAuthor:forQuestTest:Test1",questResponse[0].Id);
            Assert.AreEqual(false,questResponse[0].IsCompleted);
            Assert.AreEqual(false,questResponse[0].IsRewarded);
            Assert.AreEqual(false,questResponse[0].IsRewardEarnable);
            
            Assert.AreEqual("QuestAuthor:forQuestTest:Test2",questResponse[1].Id);
            Assert.AreEqual(true,questResponse[1].IsCompleted);
            Assert.AreEqual(false,questResponse[1].IsRewarded);
            Assert.AreEqual(false,questResponse[1].IsRewardEarnable);
            
            Assert.AreEqual("QuestAuthor:forQuestTest:Test3",questResponse[2].Id);
            Assert.AreEqual(true,questResponse[2].IsCompleted);
            Assert.AreEqual(true,questResponse[2].IsRewarded);
            Assert.AreEqual(false,questResponse[2].IsRewardEarnable);
        }
    }
}