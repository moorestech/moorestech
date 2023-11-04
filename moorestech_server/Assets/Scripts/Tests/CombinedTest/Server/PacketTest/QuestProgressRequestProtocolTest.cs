
using System.Collections.Generic;
using System.Linq;
using Game.Quest.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Test.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class QuestProgressRequestProtocolTest
    {
        private const int PlayerId = 1;

        /// <summary>
        ///     現在のクエスト進捗状況を取得するテスト
        /// </summary>
        [Test]
        public void GetTest()
        {
            //テスト用のセーブデータ

            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            //セーブデータを生成し、ロードする
            var playerQuests = new List<SaveQuestData>
            {
                new("QuestAuthor:forQuestTest:Test1", false, false),
                new("QuestAuthor:forQuestTest:Test2", true, false),
                new("QuestAuthor:forQuestTest:Test3", true, true),
                new("QuestAuthor:forQuestTest:Test4", false, false)
            };
            var quests = new Dictionary<int, List<SaveQuestData>> { { 1, playerQuests } };

            serviceProvider.GetService<IQuestDataStore>().LoadQuestDataDictionary(quests);


            //クエストのデータ要求クラス
            var payload = MessagePackSerializer.Serialize(new QuestProgressRequestProtocolMessagePack(PlayerId)).ToList();
            //データの検証
            var questResponse = MessagePackSerializer.Deserialize<QuestProgressResponseProtocolMessagePack>(packet.GetPacketResponse(payload)[0].ToArray()).Quests;

            Assert.AreEqual("QuestAuthor:forQuestTest:Test1", questResponse[0].Id);
            Assert.AreEqual(false, questResponse[0].IsCompleted);
            Assert.AreEqual(false, questResponse[0].IsRewarded);
            Assert.AreEqual(false, questResponse[0].IsRewardEarnable);

            Assert.AreEqual("QuestAuthor:forQuestTest:Test2", questResponse[1].Id);
            Assert.AreEqual(true, questResponse[1].IsCompleted);
            Assert.AreEqual(false, questResponse[1].IsRewarded);
            Assert.AreEqual(false, questResponse[1].IsRewardEarnable);

            Assert.AreEqual("QuestAuthor:forQuestTest:Test3", questResponse[2].Id);
            Assert.AreEqual(true, questResponse[2].IsCompleted);
            Assert.AreEqual(true, questResponse[2].IsRewarded);
            Assert.AreEqual(false, questResponse[2].IsRewardEarnable);
        }
    }
}