using System.Collections.Generic;
using System.Linq;
using Core.Ore;
using Game.PlayerInventory.Interface;
using Game.WorldMap;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class MiningOperationProtocolTest
    {
        [Test]
        public void MiningTest()
        {
            var PlayerId = 0;
            var oreId = 0;

            var (packet, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var oreConfig = serviceProvider.GetService<IOreConfig>();
            var seed = serviceProvider.GetService<Seed>();

            var playerInventoryData =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);

            //500×500マス内にある鉱石を探知
            var veinGenerator = serviceProvider.GetService<VeinGenerator>();


            var x = 0;
            var y = 0;

            var i = 0;
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
            Debug.Log(oreItemId);

            //プロトコルを使って鉱石を採掘
            packet.GetPacketResponse(MiningOperation(x, y, PlayerId));

            //インベントリーに鉱石がはいっているか
            var playerSlotIndex = PlayerInventoryConst.HotBarSlotToInventorySlot(0);
            Assert.AreEqual(oreItemId, playerInventoryData.MainOpenableInventory.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(1, playerInventoryData.MainOpenableInventory.GetItem(playerSlotIndex).Count);
        } //パケット

        private List<byte> MiningOperation(int x, int y, int playerId)
        {
            return MessagePackSerializer.Serialize(new MiningOperationProtocolMessagePack(playerId, x, y)).ToList();
        }
    }
}