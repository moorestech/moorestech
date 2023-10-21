#if NET6_0
using System.Collections.Generic;
using System.Linq;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest
{
    public class InitialHandshakeProtocolTest
    {
        private const int PlayerId = 1;

        [Test]
        public void SpawnCoordinateTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            
            serviceProvider.GetService<IWorldSettingsDatastore>().Initialize();

            
            var response = packet.GetPacketResponse(GetHandshakePacket(PlayerId))[0];
            var handShakeResponse = MessagePackSerializer.Deserialize<ResponseInitialHandshakeMessagePack>(response.ToArray());
            //0,0
            //SEED
            Assert.AreEqual(2, handShakeResponse.PlayerPos.X);
            Assert.AreEqual(-88, handShakeResponse.PlayerPos.Y);


            
            packet.GetPacketResponse(GetPlayerPositionPacket(PlayerId, 100, -100));


            
            response = packet.GetPacketResponse(GetHandshakePacket(PlayerId))[0];
            handShakeResponse = MessagePackSerializer.Deserialize<ResponseInitialHandshakeMessagePack>(response.ToArray());
            Assert.AreEqual(100, handShakeResponse.PlayerPos.X);
            Assert.AreEqual(-100, handShakeResponse.PlayerPos.Y);
        }

        private List<byte> GetHandshakePacket(int playerId)
        {
            return MessagePackSerializer.Serialize(
                new RequestInitialHandshakeMessagePack(playerId, "test player name")).ToList();
        }


        private List<byte> GetPlayerPositionPacket(int playerId, int x, int y)
        {
            return MessagePackSerializer.Serialize(
                new PlayerCoordinateSendProtocolMessagePack(playerId, x, y)).ToList();
        }
    }
}
#endif