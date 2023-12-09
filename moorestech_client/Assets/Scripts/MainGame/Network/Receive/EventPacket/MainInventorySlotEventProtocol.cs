using System.Collections.Generic;
using Core.Item;
using Core.Item.Config;
using Cysharp.Threading.Tasks;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.UnityView.UI.Inventory;
using MessagePack;
using Server.Event.EventReceive;

namespace MainGame.Network.Receive.EventPacket
{
    public class MainInventorySlotEventProtocol : IAnalysisEventPacket
    {
        private readonly ItemStackFactory _itemStackFactory;
        private readonly InventoryMainAndSubCombineItems _inventoryMainAndSubCombineItems;

        public MainInventorySlotEventProtocol(ItemStackFactory itemStackFactory,InventoryMainAndSubCombineItems inventoryMainAndSubCombineItems)
        {
            _inventoryMainAndSubCombineItems = inventoryMainAndSubCombineItems;
            _itemStackFactory = itemStackFactory;
        }

        public void Analysis(List<byte> packet)
        {
            var data = MessagePackSerializer
                .Deserialize<MainInventoryUpdateEventMessagePack>(packet.ToArray());

            _inventoryMainAndSubCombineItems[data.Slot] = _itemStackFactory.Create(data.Item.Id, data.Item.Count);
        }
    }
}