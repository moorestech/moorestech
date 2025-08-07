using System.Collections.Generic;
using System.Linq;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using static Server.Protocol.PacketResponse.SendCommandProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class SendCommandProtocolTest
    {
        [Test]
        public void GiveCommandTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            //送信するパケットの作成
            //ID2のアイテムを5個入れる
            var commandPacket = GetGiveCommandPacket(10, 2, 5);
            //送信を実行
            packet.GetPacketResponse(commandPacket);
            
            
            //アイテムが正しく入っているかチェック
            
            //プレイヤーインベントリを取得
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(10);
            
            //何もないインベントリに入れたのでホットバースロット0にアイテムが入っているかチェック
            var id2Slot = PlayerInventoryConst.HotBarSlotToInventorySlot(0);
            Assert.AreEqual(2, playerInventory.MainOpenableInventory.GetItem(id2Slot).Id.AsPrimitive());
            Assert.AreEqual(5, playerInventory.MainOpenableInventory.GetItem(id2Slot).Count);
            
            
            //別のアイテムIDを入れたので、ホットバースロット1にアイテムが入っているかチェック
            packet.GetPacketResponse(GetGiveCommandPacket(10, 3, 7));
            var id3Slot = PlayerInventoryConst.HotBarSlotToInventorySlot(1);
            Assert.AreEqual(3, playerInventory.MainOpenableInventory.GetItem(id3Slot).Id.AsPrimitive());
            Assert.AreEqual(7, playerInventory.MainOpenableInventory.GetItem(id3Slot).Count);
            
            //アイテムID2を入れたので、ホットバースロット0のアイテムが増えているかチェック
            packet.GetPacketResponse(GetGiveCommandPacket(10, 2, 3));
            Assert.AreEqual(2, playerInventory.MainOpenableInventory.GetItem(id2Slot).Id.AsPrimitive());
            Assert.AreEqual(8, playerInventory.MainOpenableInventory.GetItem(id2Slot).Count);
        }
        
        private List<byte> GetGiveCommandPacket(int playerId, int itemId, int count)
        {
            var giveCommand = $"give {playerId} {itemId} {count}"; //give <playerId> <itemId> <count>
            
            
            return MessagePackSerializer.Serialize(new SendCommandProtocolMessagePack(giveCommand)).ToList();
        }
    }
}