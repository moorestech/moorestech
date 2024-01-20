using System.Collections.Generic;
using Core.Item;
using Cysharp.Threading.Tasks;
using MainGame.UnityView.UI.Inventory.Main;
using MessagePack;
using Server.Event.EventReceive;

namespace MainGame.Network.Receive.EventPacket
{
    public class MainInventorySlotEventProtocol : IAnalysisEventPacket
    {
        private readonly ItemStackFactory _itemStackFactory;
        private readonly LocalPlayerInventory _localPlayerInventory;

        public MainInventorySlotEventProtocol(ItemStackFactory itemStackFactory,LocalPlayerInventory localPlayerInventory)
        {
            _localPlayerInventory = localPlayerInventory;
            _itemStackFactory = itemStackFactory;
        }

        public void Analysis(List<byte> packet)
        {
            var data = MessagePackSerializer.Deserialize<MainInventoryUpdateEventMessagePack>(packet.ToArray());

            SetItemData(data).Forget();
        }
        
        private async UniTask SetItemData(MainInventoryUpdateEventMessagePack data)
        {
            await UniTask.SwitchToMainThread();
            
            _localPlayerInventory[data.Slot] = _itemStackFactory.Create(data.Item.Id, data.Item.Count);
        }
    }
}