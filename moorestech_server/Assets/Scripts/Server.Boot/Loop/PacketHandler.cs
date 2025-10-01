using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Server.Protocol;
using UnityEngine;

namespace Server.Boot.Loop
{
    public class PacketHandler : IDisposable
    {
        private const int Port = 11564;
        private Socket _listener;

        public void StartServer(PacketResponseCreator packetResponseCreator, CancellationToken token)
        {
            //ソケットの作成
            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //通信の受け入れ準備
            _listener.Bind(new IPEndPoint(IPAddress.Any, Port));
            _listener.Listen(10);
            Debug.Log("moorestechサーバー 起動完了");

            try
            {
                while (true)
                {
                    //通信の確率
                    var client = _listener.Accept();
                    Debug.Log("接続確立");

                    var receiveThread = new Thread(() => new UserResponse(client, packetResponseCreator).StartListen(token));
                    receiveThread.Name = "[moorestech] 受信スレッド";

                    receiveThread.Start();
                }
            }
            catch (SocketException)
            {
                // listenerがCloseされた場合は正常終了
                Debug.Log("サーバーソケットがクローズされました");
            }
            catch (ObjectDisposedException)
            {
                // listenerがDisposeされた場合も正常終了
                Debug.Log("サーバーソケットが破棄されました");
            }
        }

        public void Dispose()
        {
            _listener?.Close();
            _listener?.Dispose();
        }
    }
}