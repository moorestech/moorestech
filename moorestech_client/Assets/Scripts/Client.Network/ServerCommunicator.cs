using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using MainGame.Network.Send.SocketUtil;
using MessagePack;
using Server.Util;
using UniRx;
using UnityEngine;

namespace MainGame.Network
{
    /// <summary>
    /// C#の<see cref="Socket"/>クラスを用いて実際にサーバーと通信するクラス
    /// 受信他データは<see cref="PacketExchangeManager"/>に送っている
    /// </summary>
    public class ServerCommunicator
    {
        private readonly Subject<Unit> _onDisconnect = new();
        
        private readonly Socket _socket;
        private readonly IPAddress _ipAddress;
        
        public static async UniTask<ServerCommunicator> CreateConnectedInstance(ConnectionServerConfig connectionServerConfig)
        {
            //IPアドレスやポートを設定
            if (!IPAddress.TryParse(connectionServerConfig.IP, out var ipAddress))
            {
                throw new ArgumentException("IP解析失敗");
            }

            var socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            //接続を行う
            socket.Connect(ipAddress, connectionServerConfig.Port);

            // 接続に10秒かかったらエラーを出す
            await UniTask.WaitUntil(() => socket.Connected).Timeout(TimeSpan.FromSeconds(10));

            Debug.Log("サーバーに接続しました");
            
            return new ServerCommunicator(socket);
        }
        
        private ServerCommunicator(Socket connectedSocket)
        {
            //ソケットを作成
            _socket = connectedSocket;
        }

        public IObservable<Unit> OnDisconnect => _onDisconnect;


        public Task StartCommunicat(PacketExchangeManager packetExchangeManager)
        {
            var buffer = new byte[4096];

            var parser = new PacketBufferParser();
            try
            {
                while (true)
                {
                    //Receiveで受信
                    var length = _socket.Receive(buffer);
                    if (length == 0)
                    {
                        Debug.LogError("ストリームがゼロによる切断");
                        break;
                    }

                    //解析をしてunity viewに送る
                    var packets = parser.Parse(buffer, length);
                    foreach (var packet in packets) packetExchangeManager.ExchangeReceivedPacket(packet).Forget();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("エラーによりサーバーから切断されました");
                Debug.LogError($"Message {e.Message} StackTrace {e.StackTrace}");
                if (_socket.Connected) _socket.Close();

                try
                {
                    var json = MessagePackSerializer.ConvertToJson(buffer);
                    Debug.LogError("受信パケット内容 JSON:" + json);
                }
                catch (Exception exception)
                {
                    Debug.LogError("受信パケット内容 JSON:解析に失敗");
                }

                throw;
            }
            finally
            {
                Debug.Log("通信ループ終了");
                InvokeDisconnect().Forget();
            }

            return Task.CompletedTask;
        }

        private async UniTask InvokeDisconnect()
        {
            await UniTask.SwitchToMainThread();
            _onDisconnect.OnNext(Unit.Default);
        }
        
        public void Send(byte[] data)
        {
            //先頭にパケット長を設定して送信
            var byteCount = ToByteList.Convert(data.Length);
            var newData = new byte[byteCount.Count + data.Length];
            
            byteCount.CopyTo(newData, 0);
            data.CopyTo(newData, byteCount.Count);
            
            _socket.Send(newData);
        }
        
        public void Close()
        {
            _socket.Close();
        }
    }
}