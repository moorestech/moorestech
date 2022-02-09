namespace MainGame.Network.Interface.Send
{
    public interface IRequestBlockInventoryProtocol
    {
        public void Send(int playerId);
    }
}