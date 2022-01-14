namespace MainGame.Network
{
    public class ConnectionServerConfig
    {
        public readonly string IP;
        public readonly int Port;

        public ConnectionServerConfig(string ip, int port)
        {
            IP = ip;
            Port = port;
        }
    }
}