using System.Collections.Generic;
using System.Linq;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Server.StartServerSystem;
using Server.Util;
using Test.Module.TestConfig;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest
{
    public class PutBlockProtocolTest
    {
        [Test]
        public void SimpleBlockPlaceTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();

            packetResponse.GetPacketResponse(BlockPlace(1, 0, 0));
            packetResponse.GetPacketResponse(BlockPlace(31, 2, 6));
            packetResponse.GetPacketResponse(BlockPlace(10, -5, 6));
            packetResponse.GetPacketResponse(BlockPlace(65, 0, -9));


            Assert.AreEqual(worldBlock.GetBlock(0, 0).BlockId, 1);
            Assert.AreEqual(worldBlock.GetBlock(2, 6).BlockId, 31);
            Assert.AreEqual(worldBlock.GetBlock(-5, 6).BlockId, 10);
            Assert.AreEqual(worldBlock.GetBlock(0, -9).BlockId, 65);
        }

        List<byte> BlockPlace(int id, int x, int y)
        {
            return MessagePackSerializer.Serialize(new PutBlockProtocolMessagePack(id, x, y)).ToList();
        }
    }
}