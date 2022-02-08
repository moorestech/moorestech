namespace MainGame.UnityView.Interface.PlayerInput
{
    public interface IBlockInventoryItemMove
    {
        public void MoveAllItemStack(int fromSlot, int toSlot,bool toBlock);
        public void MoveHalfItemStack(int fromSlot, int toSlot,bool toBlock);
        public void MoveOneItemStack(int fromSlot, int toSlot,bool toBlock);
    }
}