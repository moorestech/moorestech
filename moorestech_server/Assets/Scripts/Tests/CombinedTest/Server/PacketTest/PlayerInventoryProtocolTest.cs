using System.Linq;
using Core.Const;
using Core.Item.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class PlayerInventoryProtocolTest
    {
        [Test]
        public void GetPlayerInventoryProtocolTest()
        {
            var playerId = 1;

            var (packet, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);


            //からの時のデータ要求
            var payload = MessagePackSerializer.Serialize(new RequestPlayerInventoryProtocolMessagePack(playerId))
                .ToList();
            //データの検証
            var data = MessagePackSerializer.Deserialize<PlayerInventoryResponseProtocolMessagePack>(
                packet.GetPacketResponse(payload)[0].ToArray());
            Assert.AreEqual(playerId, data.PlayerId);

            //プレイヤーインベントリの検証
            for (var i = 0; i < PlayerInventoryConst.MainInventoryColumns; i++)
            {
                Assert.AreEqual(ItemConst.EmptyItemId, data.Main[i].Id);
                Assert.AreEqual(0, data.Main[i].Count);
            }

            //グラブインベントリの検証
            Assert.AreEqual(0, data.Grab.Id);
            Assert.AreEqual(0, data.Grab.Count);


            //インベントリにアイテムが入っている時のテスト
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);
            var itemStackFactory = ServerContext.ItemStackFactory;
            playerInventoryData.MainOpenableInventory.SetItem(0, itemStackFactory.Create(1, 5));
            playerInventoryData.MainOpenableInventory.SetItem(20, itemStackFactory.Create(3, 1));
            playerInventoryData.MainOpenableInventory.SetItem(34, itemStackFactory.Create(10, 7));


            //2回目のデータ要求
            data = MessagePackSerializer.Deserialize<PlayerInventoryResponseProtocolMessagePack>(
                packet.GetPacketResponse(payload)[0].ToArray());
            Assert.AreEqual(playerId, data.PlayerId);

            //データの検証
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
                if (i == 0)
                {
                    Assert.AreEqual(1, data.Main[i].Id);
                    Assert.AreEqual(5, data.Main[i].Count);
                }
                else if (i == 20)
                {
                    Assert.AreEqual(3, data.Main[i].Id);
                    Assert.AreEqual(1, data.Main[i].Count);
                }
                else if (i == 34)
                {
                    Assert.AreEqual(10, data.Main[i].Id);
                    Assert.AreEqual(7, data.Main[i].Count);
                }
                else
                {
                    Assert.AreEqual(ItemConst.EmptyItemId, data.Main[i].Id);
                    Assert.AreEqual(0, data.Main[i].Count);
                }
        }
    }
}