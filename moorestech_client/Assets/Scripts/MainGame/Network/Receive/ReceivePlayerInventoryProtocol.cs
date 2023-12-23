using System.Collections.Generic;
using Core.Item;
using Core.Item.Config;
using Cysharp.Threading.Tasks;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.UnityView.UI.Inventory;
using MainGame.UnityView.UI.Inventory.Main;
using MessagePack;
using Server.Protocol.PacketResponse;
using SinglePlay;

namespace MainGame.Network.Receive
{
    /// <summary>
    ///     Analysis player inventory data
    /// </summary>
    public class ReceivePlayerInventoryProtocol : IAnalysisPacket
    {
        private readonly InventoryMainAndSubCombineItems _inventoryMainAndSubCombineItems;
        private readonly LocalPlayerInventoryDataController _localPlayerInventoryDataController;
        
        private readonly ItemStackFactory _itemStackFactory;

        public ReceivePlayerInventoryProtocol(ItemStackFactory itemStackFactory, LocalPlayerInventoryDataController localPlayerInventoryDataController)
        {
            _itemStackFactory = itemStackFactory;
            _localPlayerInventoryDataController = localPlayerInventoryDataController;
            _inventoryMainAndSubCombineItems = localPlayerInventoryDataController.InventoryItems as InventoryMainAndSubCombineItems;
        }
        
        


        public void Analysis(List<byte> packet)
        {
            var data = MessagePackSerializer.Deserialize<PlayerInventoryResponseProtocolMessagePack>(packet.ToArray());
            SetItemData(data).Forget();
        }

        private async UniTask SetItemData(PlayerInventoryResponseProtocolMessagePack data)
        {
            await UniTask.SwitchToMainThread();
            
            //main inventory items
            var mainItems = new List<ItemStack>();
            for (var i = 0; i < PlayerInventoryConstant.MainInventorySize; i++)
            {
                var item = data.Main[i];
                mainItems.Add(new ItemStack(item.Id, item.Count));
                _inventoryMainAndSubCombineItems[i] = _itemStackFactory.Create(item.Id, item.Count);
            }
            
            _localPlayerInventoryDataController.SetGrabItem( _itemStackFactory.Create(data.Grab.Id, data.Grab.Count));
        }
    }
}