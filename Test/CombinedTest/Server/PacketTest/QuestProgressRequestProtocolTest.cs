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
        /// </summary>
        [Test]
        public void GetTest()
        {
            //テスト用のセーブデータを用意
            var json = "{\"world\":[],\"playerInventory\":[],\"entities\":[]}";

            var playerId = 1;
            
            //TODO テスト用のmodを用意する
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            (serviceProvider.GetService<ILoadRepository>() as LoadJsonFile).Load(json);


            //クエストのデータ要求クラス
            var payload = MessagePackSerializer.Serialize(new QuestProgressRequestProtocolMessagePack(playerId)).ToList();
            //データの検証
            var questResponse = MessagePackSerializer.Deserialize<QuestProgressResponseProtocolMessagePack>(packet.GetPacketResponse(payload)[0].ToArray());
            
            //TODO
            Assert.Fail();
            

        }
    }
}