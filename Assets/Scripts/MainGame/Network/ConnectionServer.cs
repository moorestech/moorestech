using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace MainGame.Network
{
    public class ConnectionServer
    {
        private readonly string _ip;
        private readonly int _port;
        private readonly AllReceivePacketAnalysisService _allReceivePacketAnalysisService;

        
        private Socket _socket;
        
        public ConnectionServer(ConnectionServerConfig connectionServerConfig,AllReceivePacketAnalysisService allReceivePacketAnalysisService)
        {
            _ip = connectionServerConfig.IP;
            _port = connectionServerConfig.Port;
            _allReceivePacketAnalysisService = allReceivePacketAnalysisService;
        }

        public void Connect()
        {
            //IPアドレスやポートを設定
            IPHostEntry ipHostInfo = Dns.GetHostEntry(_ip);
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, _port);


            Debug.Log("サーバーに接続します");
            //ソケットを作成
            _socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            //接続する。失敗するとエラーで落ちる。
            _socket.Connect(remoteEP);
            
            Debug.Log("サーバーに接続しました");
            byte[] bytes = new byte[4096];
            while (true)
            {
                //Receiveで受信
                var len = _socket.Receive(bytes);
                if (len == 0)
                {
                    Debug.Log("サーバーから切断されました");
                    break;
                }
                //解析を行う
                _allReceivePacketAnalysisService.Analysis(bytes);
            }
        }
    }
}
