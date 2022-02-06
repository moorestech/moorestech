using MainGame.Network.Interface.Send;
using MainGame.UnityView.Interface.PlayerInput;

namespace MainGame.GameLogic.Inventory
{
    public class PlayerInventoryItemMove : IPlayerInventoryItemMove
    {
        private ISendPlayerInventoryMoveItemProtocol _sendPlayerInventoryMoveItemProtocol;
        private InventoryDataStoreCache _inventoryDataStoreCache;


        public PlayerInventoryItemMove(ISendPlayerInventoryMoveItemProtocol sendPlayerInventoryMoveItemProtocol, InventoryDataStoreCache inventoryDataStoreCache)
        {
            _sendPlayerInventoryMoveItemProtocol = sendPlayerInventoryMoveItemProtocol;
            _inventoryDataStoreCache = inventoryDataStoreCache;
        }

        public void MoveAllItemStack(int fromSlot, int toSlot)
        {
            throw new System.NotImplementedException();
        }

        public void MoveHalfItemStack(int fromSlot, int toSlot)
        {
            throw new System.NotImplementedException();
        }

        public void MoveOneItemStack(int fromSlot, int toSlot)
        {
            throw new System.NotImplementedException();
        }
    }
}