using System.Net;
using System.Net.Sockets;
using System.Threading;
using Server.Protocol;
using UnityEngine;

namespace Server.Boot.Loop
{
    public class ServerListenAcceptor
    {
        private const int Port = 11564;
        
        public void StartServer(PacketResponseCreator packetResponseCreator, CancellationToken token)
        {
            //ソケットの作成
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //通信の受け入れ準備
            listener.Bind(new IPEndPoint(IPAddress.Any, Port));
            listener.Listen(10);
            Debug.Log("moorestechサーバー 起動完了");
            
            while (true)
            {
                //通信の確率
                var client = listener.Accept();
                Debug.Log("接続確立");
                
                var receiveThread = new Thread(() => new UserPacketHandler(client, packetResponseCreator).StartListen(token));
                receiveThread.Name = "[moorestech] 受信スレッド";
                
                receiveThread.Start();
            }
        }
    }
}