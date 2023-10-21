#if NET6_0
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest.Event
{
    /// <summary>
    ///     
    /// </summary>
    public class BlockRemoveEventPacketTest
    {
        [Test]
        public void RemoveBlockEvent()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            //ID
            var response = packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(0, response.Count);
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();


            
            BlockPlace(4, 0, 1, worldBlock, blockFactory);
            BlockPlace(3, 1, 2, worldBlock, blockFactory);
            BlockPlace(2, 3, 3, worldBlock, blockFactory);
            BlockPlace(1, 4, 4, worldBlock, blockFactory);

            
            response = packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(4, response.Count);

            var worldDataStore = serviceProvider.GetService<IWorldBlockDatastore>();
            
            worldDataStore.RemoveBlock(4, 0);

            
            response = packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(1, response.Count);
            var (x, y) = AnalysisResponsePacket(response[0]);
            Assert.AreEqual(4, x);
            Assert.AreEqual(0, y);

            
            worldDataStore.RemoveBlock(3, 1);
            worldDataStore.RemoveBlock(1, 4);
            
            response = packetResponse.GetPacketResponse(EventRequestData(0));
            Assert.AreEqual(2, response.Count);
            (x, y) = AnalysisResponsePacket(response[0]);
            Assert.AreEqual(3, x);
            Assert.AreEqual(1, y);
            (x, y) = AnalysisResponsePacket(response[1]);
            Assert.AreEqual(1, x);
            Assert.AreEqual(4, y);
        }

        private void BlockPlace(int x, int y, int id, IWorldBlockDatastore worldBlock, IBlockFactory blockFactory)
        {
            worldBlock.AddBlock(blockFactory.Create(id, new Random().Next()), x, y, BlockDirection.North);
        }

        private List<byte> EventRequestData(int plyaerID)
        {
            return MessagePackSerializer.Serialize(new EventProtocolMessagePack(plyaerID)).ToList();
        }

        private (int, int) AnalysisResponsePacket(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RemoveBlockEventMessagePack>(payload.ToArray());

            return (data.X, data.Y);
        }
    }
}
#endif