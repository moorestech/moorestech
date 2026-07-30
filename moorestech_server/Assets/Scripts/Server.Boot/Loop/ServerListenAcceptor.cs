using System.Net;
using System.Net.Sockets;
using System.Threading;
using Game.PlayerConnection;
using Server.Boot.Loop.PacketProcessing;
using Server.Event;
using Server.Protocol;
using UnityEngine;

namespace Server.Boot.Loop
{
    public class ServerListenAcceptor
    {
        // ポート未指定時の既定値。0を指定するとOSが空きポートを採番する
        // Default port when unspecified; passing 0 makes the OS assign a free port
        private const int DefaultPort = 11564;

        public static Socket CreateBoundListener(int? port)
        {
            port ??= DefaultPort;
            
            //ソケットの作成と受け入れ準備。port 0ならOSが空きポートへバインドする
            //Create the socket and start listening; port 0 binds to an OS-assigned free port
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Any, port));
            listener.Listen(10);
            return listener;
        }

        public static void StartServer(
            Socket listener,
            PacketResponseCreator packetResponseCreator,
            PlayerConnectionRegistry connectionRegistry,
            EventProtocolProvider eventProtocolProvider,
            TickEndPacketQueue tickEndPacketQueue,
            CancellationToken token)
        {
            Debug.Log($"moorestechサーバー 起動完了 port:{((IPEndPoint)listener.LocalEndPoint).Port}");

            while (true)
            {
                //通信の確立
                var client = listener.Accept();
                Debug.Log("接続確立");

                // 送信・受信キュープロセッサを作成
                var sendQueueProcessor = new SendQueueProcessor(client);
                var packetResponseContext = new PacketResponseContext(sendQueueProcessor);
                var receiveQueueProcessor = new ReceiveQueueProcessor(
                    packetResponseCreator, sendQueueProcessor, packetResponseContext, tickEndPacketQueue);

                // 受信スレッドを起動
                var receiveThread = new Thread(() => new UserPacketHandler(client, receiveQueueProcessor, sendQueueProcessor, connectionRegistry, eventProtocolProvider, packetResponseContext).StartListen(token));
                receiveThread.Name = "[moorestech] 受信スレッド";
                receiveThread.Start();
            }
        }
    }
}
