using System;
using System.Collections.Generic;
using System.Linq;
using Core.ConfigJson;
using Core.Ore;
using Core.Ore.Config;
using Game.PlayerInventory.Interface;
using Game.WorldMap;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Boot;
using Server.Protocol.PacketResponse;

using Server.Util;

using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest
{
    public class MiningOperationProtocolTest
    {

        [Test]
        public void MiningTest()
        {

            int PlayerId = 0;
            int playerSlotIndex = 0;
            var oreId = 0;
            
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var oreConfig = serviceProvider.GetService<IOreConfig>();
            var seed = serviceProvider.GetService<Seed>();
            
            var playerInventoryData =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            
            //500×500マス内にある鉱石を探知
            var veinGenerator = serviceProvider.GetService<VeinGenerator>();


            int x = 0;
            int y = 0;
            
            int i = 0;
            while (true)
            {
                oreId = veinGenerator.GetOreId(i, y);
                if (oreId != OreConst.NoneOreId)
                {
                    x = i;
                    break;
                }
                i++;
            }

            var oreItemId = oreConfig.OreIdToItemId(oreId);
            Console.WriteLine(oreItemId);
            
            //プロトコルを使って鉱石を採掘
            packet.GetPacketResponse(MiningOperation(x, y, PlayerId));
            
            //インベントリーに鉱石がはいっているか
            Assert.AreEqual(oreItemId, playerInventoryData.MainOpenableInventory.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(1, playerInventoryData.MainOpenableInventory.GetItem(playerSlotIndex).Count);

            
           




        } //パケット
        List<byte> MiningOperation(int x, int y,int playerId)
        {
            return MessagePackSerializer.Serialize(new MiningOperationProtocolMessagePack(playerId,x,y)).ToList();
        }
        
    }
}