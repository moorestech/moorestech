using Server.Boot;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest
{
    public class QuestProgressRequestProtocolTest
    {
        /// <summary>
        /// 現在のクエスト進捗状況を取得するテスト
        /// </summary>
        public void GetTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            //TODO

        }
    }
}