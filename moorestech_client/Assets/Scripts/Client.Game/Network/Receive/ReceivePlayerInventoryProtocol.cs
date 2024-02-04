using System.Collections.Generic;
using Core.Item;
using Cysharp.Threading.Tasks;
using Constant;
using Game.PlayerInventory.Interface;
using MainGame.UnityView.UI.Inventory.Main;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Receive
{
    /// <summary>
    ///     Analysis player inventory data
    /// </summary>
    public class ReceivePlayerInventoryProtocol : IAnalysisPacket
    {
        private readonly LocalPlayerInventory _localPlayerInventory;
        private readonly LocalPlayerInventoryController _localPlayerInventoryController;
        
        private readonly ItemStackFactory _itemStackFactory;

        public ReceivePlayerInventoryProtocol(ItemStackFactory itemStackFactory, LocalPlayerInventoryController localPlayerInventoryController)
        {
            _itemStackFactory = itemStackFactory;
            _localPlayerInventoryController = localPlayerInventoryController;
            _localPlayerInventory = localPlayerInventoryController.LocalPlayerInventory as LocalPlayerInventory;
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
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                var item = data.Main[i];
                mainItems.Add(new ItemStack(item.Id, item.Count));
                _localPlayerInventory[i] = _itemStackFactory.Create(item.Id, item.Count);
            }
            
            _localPlayerInventoryController.SetGrabItem( _itemStackFactory.Create(data.Grab.Id, data.Grab.Count));
        }
    }
}