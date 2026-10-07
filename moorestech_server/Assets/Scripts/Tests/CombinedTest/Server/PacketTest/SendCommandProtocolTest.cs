using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using Game.PlayerIdentity;
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
            serviceProvider.GetRequiredService<PlayerIdentityRegistry>().Load(new PlayersSaveJsonObject(11, null,
                new List<PlayerIdentityEntryJsonObject> { new(10, "steam:10") }));
            
            //送信するパケットの作成
            //ID2のアイテムを5個入れる
            var commandPacket = GetGiveCommandPacket(2, 5);
            //送信を実行
            packet.GetPacketResponse(commandPacket, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(10));
            
            
            //アイテムが正しく入っているかチェック
            
            //プレイヤーインベントリを取得
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(10);
            
            //空インベントリなのでスロット0を確認
            const int id2Slot = 0;
            Assert.AreEqual(2, playerInventory.MainOpenableInventory.GetItem(id2Slot).Id.AsPrimitive());
            Assert.AreEqual(5, playerInventory.MainOpenableInventory.GetItem(id2Slot).Count);


            //別IDなのでスロット1を確認
            packet.GetPacketResponse(GetGiveCommandPacket(3, 7), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(10));
            const int id3Slot = 1;
            Assert.AreEqual(3, playerInventory.MainOpenableInventory.GetItem(id3Slot).Id.AsPrimitive());
            Assert.AreEqual(7, playerInventory.MainOpenableInventory.GetItem(id3Slot).Count);

            //ID2追加でスロット0の増加確認
            packet.GetPacketResponse(GetGiveCommandPacket(2, 3), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(10));
            Assert.AreEqual(2, playerInventory.MainOpenableInventory.GetItem(id2Slot).Id.AsPrimitive());
            Assert.AreEqual(8, playerInventory.MainOpenableInventory.GetItem(id2Slot).Count);
        }

        // 対象は接続に紐づいた要求元だけ。同じコマンドを別接続から送っても他人には届かない（ADR 0073）
        // The target is only the requester bound to the connection, so the same command never reaches another player (ADR 0073)
        [Test]
        public void Giveは要求元のインベントリにだけ入る()
        {
            var (packet, provider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            provider.GetRequiredService<PlayerIdentityRegistry>().Load(new PlayersSaveJsonObject(12, null,
                new List<PlayerIdentityEntryJsonObject> { new(10, "steam:10"), new(11, "steam:11") }));

            packet.GetPacketResponse(GetGiveCommandPacket(2, 5), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(11));

            var inventories = provider.GetService<IPlayerInventoryDataStore>();
            Assert.AreEqual(2, inventories.GetInventoryData(11).MainOpenableInventory.GetItem(0).Id.AsPrimitive());
            Assert.AreEqual(5, inventories.GetInventoryData(11).MainOpenableInventory.GetItem(0).Count);
            Assert.AreEqual(0, inventories.GetInventoryData(10).MainOpenableInventory.GetItem(0).Count);
        }

        // ClearInventoryも要求元のインベントリだけを空にする
        // ClearInventory likewise empties only the requester's inventory
        [Test]
        public void ClearInventoryは要求元のインベントリだけを空にする()
        {
            var (packet, provider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            provider.GetRequiredService<PlayerIdentityRegistry>().Load(new PlayersSaveJsonObject(12, null,
                new List<PlayerIdentityEntryJsonObject> { new(10, "steam:10"), new(11, "steam:11") }));
            packet.GetPacketResponse(GetGiveCommandPacket(2, 5), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(10));
            packet.GetPacketResponse(GetGiveCommandPacket(2, 5), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(11));

            var command = MessagePackSerializer.Serialize(new SendCommandProtocolMessagePack(ClearInventoryCommand));
            packet.GetPacketResponse(command, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(11));

            var inventories = provider.GetService<IPlayerInventoryDataStore>();
            Assert.AreEqual(0, inventories.GetInventoryData(11).MainOpenableInventory.GetItem(0).Count);
            Assert.AreEqual(5, inventories.GetInventoryData(10).MainOpenableInventory.GetItem(0).Count);
        }
        
        private byte[] GetGiveCommandPacket(int itemId, int count)
        {
            var giveCommand = $"{GiveCommand} {itemId} {count}"; //give <itemId> <count>
            
            
            return MessagePackSerializer.Serialize(new SendCommandProtocolMessagePack(giveCommand));
        }
    }
}
