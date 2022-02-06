using MainGame.Network.Interface.Send;
using MainGame.UnityView.Interface.PlayerInput;

namespace MainGame.GameLogic.Inventory
{
    
    public class PlayerInventoryItemMove : IPlayerInventoryItemMove
    {
        private readonly int _playerId;
        private readonly ISendPlayerInventoryMoveItemProtocol _sendPlayerInventoryMoveItemProtocol;
        private readonly InventoryDataStoreCache _inventoryDataStoreCache;


        public PlayerInventoryItemMove(
            ISendPlayerInventoryMoveItemProtocol sendPlayerInventoryMoveItemProtocol, 
            InventoryDataStoreCache inventoryDataStoreCache,
            ConnectionPlayerSetting connectionPlayerSetting)
        {
            _sendPlayerInventoryMoveItemProtocol = sendPlayerInventoryMoveItemProtocol;
            _inventoryDataStoreCache = inventoryDataStoreCache;
            _playerId = connectionPlayerSetting.PlayerId;
        }

        public void MoveAllItemStack(int fromSlot, int toSlot)
        {
            _sendPlayerInventoryMoveItemProtocol.Send(
                _playerId, fromSlot, toSlot, _inventoryDataStoreCache.GetItem(fromSlot).Count);
        }

        public void MoveHalfItemStack(int fromSlot, int toSlot)
        { 
            _sendPlayerInventoryMoveItemProtocol.Send(
                _playerId, fromSlot, toSlot, _inventoryDataStoreCache.GetItem(fromSlot).Count / 2);
        }

        public void MoveOneItemStack(int fromSlot, int toSlot)
        {
            _sendPlayerInventoryMoveItemProtocol.Send(_playerId, fromSlot, toSlot, 1);
        }
    }
}