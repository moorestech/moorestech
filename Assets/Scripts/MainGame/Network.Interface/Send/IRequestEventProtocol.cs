namespace MainGame.Network.Interface.Send
{
    public interface IRequestEventProtocol
    {
        public void Send(int playerId);
    }
}