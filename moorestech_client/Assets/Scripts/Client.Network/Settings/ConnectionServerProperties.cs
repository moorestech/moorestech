namespace Client.Network.Settings
{
    public class ConnectionServerProperties
    {
        public readonly string IP;
        public readonly int Port;
        
        public ConnectionServerProperties(string ip, int port)
        {
            IP = ip;
            Port = port;
        }
    }
}