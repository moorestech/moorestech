using System.Net;
using System.Net.Sockets;
using MainGame.Network.Interface;
using MainGame.Network.Send;
using UnityEngine;

namespace MainGame.Network
{
    public class ConnectionServer
    {
        private readonly string _ip;
        private readonly int _port;
        private readonly AllReceivePacketAnalysisService _allReceivePacketAnalysisService;

        private readonly SocketObject _socketObject;
        
        public ConnectionServer(
            ConnectionServerConfig connectionServerConfig,
            AllReceivePacketAnalysisService allReceivePacketAnalysisService,
            ISocket socket)
        {
            _ip = connectionServerConfig.IP;
            _port = connectionServerConfig.Port;
            _allReceivePacketAnalysisService = allReceivePacketAnalysisService;
            _socketObject = socket as SocketObject;
        }

        public void Connect()
        {
            //IPアドレスやポートを設定
            IPHostEntry ipHostInfo = Dns.GetHostEntry(_ip);
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, _port);


            Debug.Log("サーバーに接続します");
            //ソケットを作成
            var socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _socketObject.SetSocket(socket);
            //接続する。失敗するとエラーで落ちる。
            socket.Connect(remoteEP);
            
            Debug.Log("サーバーに接続しました");
            byte[] bytes = new byte[4096];
            while (true)
            {
                //Receiveで受信
                var len = socket.Receive(bytes);
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
