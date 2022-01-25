using System.Net;
using System.Net.Sockets;

namespace MainGame.Network.Send.SocketUtil
{
    public class SocketInstanceCreate
    {
        private readonly Socket _socket;
        private readonly IPEndPoint _remoteEndPoint;
        public SocketInstanceCreate(ConnectionServerConfig config)
        {
            
            //IPアドレスやポートを設定
            IPHostEntry ipHostInfo = Dns.GetHostEntry(config.IP);
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            _remoteEndPoint = new IPEndPoint(ipAddress, config.Port);

            //ソケットを作成
            _socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }
        
        public Socket GetSocket()
        {
            return _socket;
        }
        
        public IPEndPoint GetRemoteEndPoint()
        {
            return _remoteEndPoint;
        }
        
    }
}