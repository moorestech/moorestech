using System.Collections.Generic;
using Core.Item;
using Cysharp.Threading.Tasks;
using Constant;
using MainGame.Network.Event;
using MainGame.UnityView.UI.Inventory;
using MainGame.UnityView.UI.Inventory.Main;
using MessagePack;
using Server.Event.EventReceive;

namespace MainGame.Network.Receive.EventPacket
{
    public class GrabInventoryUpdateEventProtocol : IAnalysisEventPacket
    {
        private readonly LocalPlayerInventoryController _localPlayerInventoryController;
        private readonly ItemStackFactory _itemStackFactory;
        
        public GrabInventoryUpdateEventProtocol(LocalPlayerInventoryController localPlayerInventoryController, ItemStackFactory itemStackFactory)
        {
            _localPlayerInventoryController = localPlayerInventoryController;
            _itemStackFactory = itemStackFactory;
        }

        
        public void Analysis(List<byte> packet)
        {
            var data = MessagePackSerializer
                .Deserialize<GrabInventoryUpdateEventMessagePack>(packet.ToArray());
            
            _localPlayerInventoryController.SetGrabItem(_itemStackFactory.Create(data.Item.Id, data.Item.Count));
        }
    }
}