namespace MainGame.Network.Interface.Send
{
    public interface IRequestPlayerInventoryProtocol
    {
        public void Send(int playerId);
    }
}