using System.Net;
using System.Net.Sockets;
using System.Threading;
using Server.Boot.Loop.PacketProcessing;
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
            Debug.Log("moorestech server startup complete");

            while (true)
            {
                //通信の確立
                var client = listener.Accept();
                Debug.Log("Connection established");

                // 送信・受信キュープロセッサを作成
                var sendQueueProcessor = new SendQueueProcessor(client);
                var receiveQueueProcessor = new ReceiveQueueProcessor(packetResponseCreator, sendQueueProcessor);

                // 受信スレッドを起動
                var receiveThread = new Thread(() => new UserPacketHandler(client, receiveQueueProcessor, sendQueueProcessor).StartListen(token));
                receiveThread.Name = "[moorestech] Receive thread";
                receiveThread.Start();
            }
        }
    }
}