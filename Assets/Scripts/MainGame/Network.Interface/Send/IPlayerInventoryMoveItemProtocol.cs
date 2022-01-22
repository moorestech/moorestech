namespace MainGame.Network.Interface.Send
{
    public interface IPlayerInventoryMoveItemProtocol
    {
        public void Send(int playerId, int fromSlot, int toSlot,int itemCount);
    }
}