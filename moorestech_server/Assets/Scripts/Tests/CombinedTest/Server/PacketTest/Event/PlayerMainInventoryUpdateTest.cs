using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Tests.Module.TestMod;
using static Server.Protocol.PacketResponse.EventProtocol;
using static Server.Protocol.PacketResponse.InventoryItemMoveProtocol;
using Server.Protocol;
using static Server.Util.MessagePack.InventoryIdentifierMessagePack;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class PlayerMainInventoryUpdateTest
    {
        private const int PlayerId = 0;
        
        [Test]
        public void UpdateTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var response = packetResponse.GetPacketResponseForTest(EventRequestData(0), new PacketResponseContext());
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0]);
            Assert.AreEqual(0, eventMessagePack.Events.Count);
            
            //インベントリにアイテムを追加
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0);
            var itemStackFactory = ServerContext.ItemStackFactory;
            playerInventoryData.MainOpenableInventory.SetItem(5, itemStackFactory.Create(new ItemId(1), 5));
            
            //追加時のイベントのキャッチ
            response = packetResponse.GetPacketResponseForTest(EventRequestData(PlayerId), new PacketResponseContext());
            eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0]);
            var data = MessagePackSerializer.Deserialize<MainInventoryUpdateEventMessagePack>(eventMessagePack.Events[0].Payload);
            Assert.AreEqual(5, data.Slot);
            Assert.AreEqual(1, data.Item.Id.AsPrimitive());
            Assert.AreEqual(5, data.Item.Count);
            
            
            //インベントリ内のアイテムの移動を実際に移動のプロトコルを用いてテストする
            //分割のイベントのテスト
            packetResponse.GetPacketResponseForTest(PlayerInventoryItemMove(true, 5, 3), new PacketResponseContext());
            packetResponse.GetPacketResponseForTest(PlayerInventoryItemMove(false, 4, 3), new PacketResponseContext());
            
            response = packetResponse.GetPacketResponseForTest(EventRequestData(PlayerId), new PacketResponseContext());
            eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0]);
            
            Assert.AreEqual(4, eventMessagePack.Events.Count);
            
            var grabUp = MessagePackSerializer.Deserialize<GrabInventoryUpdateEventMessagePack>(eventMessagePack.Events[0].Payload);
            var setMainInventory = MessagePackSerializer.Deserialize<MainInventoryUpdateEventMessagePack>(eventMessagePack.Events[1].Payload);
            var outMainInventory = MessagePackSerializer.Deserialize<MainInventoryUpdateEventMessagePack>(eventMessagePack.Events[2].Payload);
            var grabDown = MessagePackSerializer.Deserialize<GrabInventoryUpdateEventMessagePack>(eventMessagePack.Events[3].Payload);
            
            Assert.AreEqual(5, setMainInventory.Slot); //移動時のスロット確認
            Assert.AreEqual(4, outMainInventory.Slot);
            
            Assert.AreEqual(1, grabUp.Item.Id.AsPrimitive()); //アイテムIDの確認
            Assert.AreEqual(1, setMainInventory.Item.Id.AsPrimitive());
            Assert.AreEqual(1, outMainInventory.Item.Id.AsPrimitive());
            Assert.AreEqual(0, grabDown.Item.Id.AsPrimitive());
            
            Assert.AreEqual(3, grabUp.Item.Count); //アイテム数の確認
            Assert.AreEqual(2, setMainInventory.Item.Count);
            Assert.AreEqual(3, outMainInventory.Item.Count);
            Assert.AreEqual(0, grabDown.Item.Count);
            
            
            //合成のテスト
            packetResponse.GetPacketResponseForTest(PlayerInventoryItemMove(true, 4, 3), new PacketResponseContext());
            packetResponse.GetPacketResponseForTest(PlayerInventoryItemMove(false, 5, 3), new PacketResponseContext());
            
            response = packetResponse.GetPacketResponseForTest(EventRequestData(PlayerId), new PacketResponseContext());
            eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0]);
            
            Assert.AreEqual(4, eventMessagePack.Events.Count);
            grabUp = MessagePackSerializer.Deserialize<GrabInventoryUpdateEventMessagePack>(eventMessagePack.Events[0].Payload);
            setMainInventory = MessagePackSerializer.Deserialize<MainInventoryUpdateEventMessagePack>(eventMessagePack.Events[1].Payload);
            outMainInventory = MessagePackSerializer.Deserialize<MainInventoryUpdateEventMessagePack>(eventMessagePack.Events[2].Payload);
            grabDown = MessagePackSerializer.Deserialize<GrabInventoryUpdateEventMessagePack>(eventMessagePack.Events[3].Payload);
            
            Assert.AreEqual(4, setMainInventory.Slot); //移動時のスロット確認
            Assert.AreEqual(5, outMainInventory.Slot);
            
            Assert.AreEqual(1, grabUp.Item.Id.AsPrimitive()); //アイテムIDの確認
            Assert.AreEqual(0, setMainInventory.Item.Id.AsPrimitive());
            Assert.AreEqual(1, outMainInventory.Item.Id.AsPrimitive());
            Assert.AreEqual(0, grabDown.Item.Id.AsPrimitive());
            
            Assert.AreEqual(3, grabUp.Item.Count); //アイテム数の確認
            Assert.AreEqual(0, setMainInventory.Item.Count);
            Assert.AreEqual(5, outMainInventory.Item.Count);
            Assert.AreEqual(0, grabDown.Item.Count);
        }
        
        
        private byte[] EventRequestData(int plyaerID)
        {
            return MessagePackSerializer.Serialize(new EventProtocolMessagePack(plyaerID));
            ;
        }
        
        private byte[] PlayerInventoryItemMove(bool toGrab, int inventorySlot, int itemCount)
        {
            InventoryItemMoveProtocolMessagePack messagePack;
            if (toGrab)
            {
                var from = CreateMainMessage(PlayerId);
                var to = CreateGrabMessage(PlayerId);
                messagePack = new InventoryItemMoveProtocolMessagePack(itemCount, ItemMoveType.SwapSlot,
                    from, inventorySlot, to, 0);
            }
            else
            {
                var from = CreateGrabMessage(PlayerId);
                var to = CreateMainMessage(PlayerId);
                messagePack = new InventoryItemMoveProtocolMessagePack(itemCount, ItemMoveType.SwapSlot,
                    from, 0, to, inventorySlot);
            }
            
            return MessagePackSerializer.Serialize(messagePack);
        }
    }
}
