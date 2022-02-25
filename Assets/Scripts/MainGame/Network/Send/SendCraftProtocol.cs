namespace MainGame.Network.Send
{
    public class SendCraftProtocol
    {
        private const short ProtocolId = 14;
        private readonly ISocket _socket;
        private readonly int _playerId;

        public SendCraftProtocol(ISocket socket,PlayerConnectionSetting playerConnection)
        {
            _playerId = playerConnection.PlayerId;
            _socket = socket;
        }
        
        public void Send()
        {
            //TODO
            
        }
    }
}