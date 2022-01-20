namespace MainGame.Network.Interface.Send
{
    public interface ISendPlaceHotBarBlockProtocol
    {
        public void Send(int x, int y, int hotBarSlot);
    }
}