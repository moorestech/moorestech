namespace MainGame.Network.Interface.Send
{
    public interface ISendPlayerInventoryMoveItemProtocol
    {
        public void Send(int playerId, int fromSlot, int toSlot,int itemCount);
    }
}