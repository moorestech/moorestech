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
        /// <summary>
        /// 現在のクエスト進捗状況を取得するテスト
        /// note クエストの内容はTestQuestConfig.jsonと同じです
        /// </summary>
        [Test]
        public void GetTest()
        {
            //TODO テスト用のセーブデータを用意
            var json = "{\"world\":[],\"playerInventory\":[],\"entities\":[]}";

            var playerId = 1;
            
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.QuestTestModDirectory);
            (serviceProvider.GetService<ILoadRepository>() as LoadJsonFile).Load(json);


            //クエストのデータ要求クラス
            var payload = MessagePackSerializer.Serialize(new QuestProgressRequestProtocolMessagePack(playerId)).ToList();
            //データの検証
            var questResponse = MessagePackSerializer.Deserialize<QuestProgressResponseProtocolMessagePack>(packet.GetPacketResponse(payload)[0].ToArray()).Quests;
            
            Assert.AreEqual("Test1",questResponse[0].Id);
            Assert.AreEqual(false,questResponse[0].IsCompleted);
            Assert.AreEqual(false,questResponse[0].IsRewarded);
            
            Assert.AreEqual("Test2",questResponse[0].Id);
            Assert.AreEqual(true,questResponse[0].IsCompleted);
            Assert.AreEqual(false,questResponse[0].IsRewarded);
            
            Assert.AreEqual("Test3",questResponse[0].Id);
            Assert.AreEqual(true,questResponse[0].IsCompleted);
            Assert.AreEqual(true,questResponse[0].IsRewarded);
        }
    }
}