using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Game.PlayerConnection;
using Game.SaveLoad.Snapshot;
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

        // keep-aliveの間隔はOS既定のまま（数時間）。接続ごとに出すとログが埋まるので断り書きは1度だけ出す
        // The keep-alive interval stays at the OS default (hours); the caveat is logged once, since per-connection logging would bury the log
        private static bool _keepAliveDefaultIntervalLogged;

        public static Socket CreateBoundListener(int? argPort)
        {
            var port = argPort ?? DefaultPort;

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
            ReceivedPacketLog receivedPacketLog,
            CancellationToken token)
        {
            // 接続が来なくてもcancelを観測し、破棄側のJoinを完了させる
            // Observe cancellation without an incoming connection so disposal can join the thread
            while (!token.IsCancellationRequested)
            {
                if (!listener.Poll(100_000, SelectMode.SelectRead) || token.IsCancellationRequested) continue;
                //通信の確立
                var client = listener.Accept();
                Debug.Log("接続確立");

                // 送信の無い放置接続でもOSが死活を確かめる。無いと落ちた端末の身元がサーバー再起動まで接続中のまま残る
                // Keep-alive lets the OS probe an idle connection; without it a dead peer's identity stays "connected" until the server restarts
                EnableKeepAlive(client);

                // 送信・受信キュープロセッサを作成
                var sendQueueProcessor = new SendQueueProcessor(client);
                var packetResponseContext = new PacketResponseContext(sendQueueProcessor);
                var receiveQueueProcessor = new ReceiveQueueProcessor(
                    packetResponseCreator, sendQueueProcessor, packetResponseContext, tickEndPacketQueue, receivedPacketLog);

                // 受信スレッドを起動
                var receiveThread = new Thread(() => new UserPacketHandler(client, receiveQueueProcessor, sendQueueProcessor, connectionRegistry, eventProtocolProvider, packetResponseContext, receivedPacketLog, tickEndPacketQueue).StartListen(token));
                receiveThread.Name = "[moorestech] 受信スレッド";
                receiveThread.Start();
            }

            #region Internal

            void EnableKeepAlive(Socket client)
            {
                // ソケット設定は外部境界。設定できない環境でも受け入れそのものは続ける
                // Socket options are an external boundary; acceptance continues even where they cannot be set
                try
                {
                    client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"接続のkeep-alive有効化に失敗しました。放置接続の死活検知が効きません: {e.Message}");
                    return;
                }

                // 間隔の短縮はこのランタイムでは指定できない（TcpKeepAliveTime/Interval が無く、IOControlのKeepAliveValuesはWindows専用）
                // Shortening the interval is not expressible on this runtime: TcpKeepAliveTime/Interval are absent and IOControl's KeepAliveValues is Windows-only
                // 検知はOS既定（数時間）まで遅れる。その間は落ちた端末と同じ身元の再接続がAlreadyConnectedで拒否される
                // Detection is therefore delayed to the OS default (hours); until then a dead peer's identity is refused with AlreadyConnected
                if (_keepAliveDefaultIntervalLogged) return;
                _keepAliveDefaultIntervalLogged = true;
                Debug.Log("接続のkeep-aliveはOS既定の間隔（数時間）で動きます。送信の無い放置接続の死活検知はそれまで遅れます");
            }

            #endregion
        }
    }
}
