using System.Collections.Generic;
using Core.Item;
using Cysharp.Threading.Tasks;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.UnityView.UI.Inventory;
using MainGame.UnityView.UI.Inventory.Main;
using MessagePack;
using Server.Event.EventReceive;

namespace MainGame.Network.Receive.EventPacket
{
    public class GrabInventoryUpdateEventProtocol : IAnalysisEventPacket
    {
        private readonly LocalPlayerInventoryDataController _localPlayerInventoryDataController;
        private readonly ItemStackFactory _itemStackFactory;
        
        public GrabInventoryUpdateEventProtocol(LocalPlayerInventoryDataController localPlayerInventoryDataController, ItemStackFactory itemStackFactory)
        {
            _localPlayerInventoryDataController = localPlayerInventoryDataController;
            _itemStackFactory = itemStackFactory;
        }

        
        public void Analysis(List<byte> packet)
        {
            var data = MessagePackSerializer
                .Deserialize<GrabInventoryUpdateEventMessagePack>(packet.ToArray());
            
            _localPlayerInventoryDataController.SetGrabItem(_itemStackFactory.Create(data.Item.Id, data.Item.Count));
        }
    }
}