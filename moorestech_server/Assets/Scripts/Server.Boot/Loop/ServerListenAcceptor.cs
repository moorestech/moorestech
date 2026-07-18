using System.Net;
using System.Net.Sockets;
using System.Threading;
using Game.PlayerConnection;
using Server.Boot.Loop.PacketProcessing;
using Server.Protocol;
using UnityEngine;

namespace Server.Boot.Loop
{
    public class ServerListenAcceptor
    {
        private const int Port = 11564;

        public void StartServer(PacketResponseCreator packetResponseCreator, PlayerConnectionRegistry connectionRegistry, CancellationToken token)
        {
            //ソケットの作成
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //通信の受け入れ準備
            listener.Bind(new IPEndPoint(IPAddress.Any, Port));
            listener.Listen(10);
            Debug.Log("moorestechサーバー 起動完了");

            while (true)
            {
                //通信の確立
                var client = listener.Accept();
                Debug.Log("接続確立");

                // 送信・受信キュープロセッサを作成
                var packetResponseContext = new PacketResponseContext();
                var sendQueueProcessor = new SendQueueProcessor(client);
                // イベントpush用のsinkを接続生成時に配線する（受信スレッド起動前）
                // Wire the event push sink at connection creation, before the receive thread starts
                packetResponseContext.SetEventSink(new ConnectionPlayerEventSink(sendQueueProcessor));
                var receiveQueueProcessor = new ReceiveQueueProcessor(packetResponseCreator, sendQueueProcessor, packetResponseContext);

                // 受信スレッドを起動
                var receiveThread = new Thread(() => new UserPacketHandler(client, receiveQueueProcessor, sendQueueProcessor, connectionRegistry, packetResponseContext).StartListen(token));
                receiveThread.Name = "[moorestech] 受信スレッド";
                receiveThread.Start();
            }
        }
    }
}
