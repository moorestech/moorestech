namespace MainGame.UnityView.Interface.PlayerInput
{
    public interface IPlayerInventoryItemMove
    {
        public void MoveAllItemStack(int fromSlot, int toSlot);
        public void MoveHalfItemStack(int fromSlot, int toSlot);
        public void MoveOneItemStack(int fromSlot, int toSlot);
    }
}