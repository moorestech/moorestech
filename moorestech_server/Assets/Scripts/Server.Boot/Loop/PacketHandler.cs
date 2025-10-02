using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Server.Protocol;
using UnityEngine;

namespace Server.Boot.Loop
{
    public class PacketHandler
    {
        private const int Port = 11564;
        
        public void StartServer(PacketResponseCreator packetResponseCreator, CancellationToken token)
        {
            //ソケットの作成
            Socket listener = null;
            CancellationTokenRegistration registration = default;
            try
            {
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                //通信の受け入れ準備
                listener.Bind(new IPEndPoint(IPAddress.Any, Port));
                listener.Listen(10);
                Debug.Log("moorestechサーバー 起動完了");

                registration = token.Register(() =>
                {
                    try
                    {
                        listener.Close();
                    }
                    catch (Exception)
                    {
                        // 想定内の例外なのでログ出力しない
                    }
                });

                while (!token.IsCancellationRequested)
                {
                    Socket client;
                    try
                    {
                        client = listener.Accept();
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (SocketException socketException) when (socketException.SocketErrorCode == SocketError.Interrupted || socketException.SocketErrorCode == SocketError.OperationAborted)
                    {
                        break;
                    }

                    if (token.IsCancellationRequested)
                    {
                        client.Close();
                        break;
                    }

                    Debug.Log("接続確立");

                    var receiveThread = new Thread(() => new UserResponse(client, packetResponseCreator).StartListen(token));
                    receiveThread.Name = "[moorestech] 受信スレッド";

                    receiveThread.Start();
                }
            }
            catch (ThreadAbortException)
            {
                Thread.ResetAbort();
            }
            finally
            {
                registration.Dispose();
                listener?.Close();
            }
        }
    }
}
