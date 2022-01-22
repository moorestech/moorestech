namespace MainGame.Network.Interface.Send
{
    public interface ISendBlockInventoryMoveItemProtocol
    {
        public void Send(int x,int y,int fromSlot,int toSlot,int itemCount);
    }
}