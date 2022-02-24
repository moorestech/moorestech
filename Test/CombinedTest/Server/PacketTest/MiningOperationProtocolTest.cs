using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Core.Block.BlockInventory;
using Core.Ore;
using Core.Ore.Config;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using Game.WorldMap;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Util;

namespace Test.CombinedTest.Server.PacketTest
{
    public class MiningOperationProtocolTest
    {

        [Test]
        public void MiningTest()
        {

            int i = 0;
            int j = 0;
            int PlayerId = 0;
            int playerSlotIndex = 0;
            var oreId = 0;
            
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var oreConfig = serviceProvider.GetService<IOreConfig>();
            var seed = serviceProvider.GetService<Seed>();
            
            var playerInventoryData =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            
            //100×100マス内にある鉱石を探知
            var veinGenerator = new VeinGenerator(new Seed(seed.SeedValue),new OreConfig());

            
            for (i = 0; i < 500; i++)
            {
                for (j = 0; j < 500; j++)
                {
                    oreId = veinGenerator.GetOreId(i, j);
                    if (oreId != OreConst.NoneOreId)
                    {
                        
                        break;
                    }
                }
                if (oreId != OreConst.NoneOreId)
                {
                    break;
                }
            }

            var oreItemId = oreConfig.OreIdToItemId(oreId);
            
            //プロトコルを使って鉱石を採掘
            packet.GetPacketResponse(MiningOperation(i, j, PlayerId));
            
            //インベントリーに鉱石がはいっているか
            Assert.AreEqual(oreItemId, playerInventoryData.MainOpenableInventory.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(1, playerInventoryData.MainOpenableInventory.GetItem(playerSlotIndex).Count);

            //ストップウォッチ（一時退避中）
            //System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            
            
            //パケット
           List<byte> MiningOperation(int x, int y,int playerId)
            {
                var payload = new List<byte>();
                payload.AddRange(ToByteList.Convert((short) 15));
                payload.AddRange(ToByteList.Convert(x));
                payload.AddRange(ToByteList.Convert(y));
                payload.AddRange(ToByteList.Convert(playerId));

                return payload;
            }




        }
    }
}