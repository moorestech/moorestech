using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Client.Network.API;
using Client.Network.Settings;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Util;
using UniRx;
using UnityEngine;

namespace Client.Network
{
    /// <summary>
    ///     C#の<see cref="Socket" />クラスを用いて実際にサーバーと通信するクラス
    ///     受信他データは<see cref="PacketExchangeManager" />に送っている
    /// </summary>
    public class ServerCommunicator
    {
        private readonly IPAddress _ipAddress;
        private readonly Subject<Unit> _onDisconnect = new();
        

        private readonly Socket _socket;
        private CancellationTokenSource _cancellationTokenSource;

        private ServerCommunicator(Socket connectedSocket)
        {
            //ソケットを作成
            _socket = connectedSocket;
            _cancellationTokenSource = new CancellationTokenSource();
        }
        
        public IObservable<Unit> OnDisconnect => _onDisconnect;
        
        public static async UniTask<ServerCommunicator> CreateConnectedInstance(ConnectionServerProperties connectionServerProperties)
        {
            //IPアドレスやポートを設定
            if (!IPAddress.TryParse(connectionServerProperties.IP, out var ipAddress)) throw new ArgumentException("IP解析失敗");
            
            var socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            
            //接続を行う
            socket.Connect(ipAddress, connectionServerProperties.Port);
            
            // 接続に10秒かかったらエラーを出す
            await UniTask.WaitUntil(() => socket.Connected).Timeout(TimeSpan.FromSeconds(10));
            
            Debug.Log("サーバーに接続しました");
            
            return new ServerCommunicator(socket);
        }
        
        
        public void StartCommunicat(PacketExchangeManager packetExchangeManager)
        {
            Task.Run(() => CommunicationLoop(packetExchangeManager, _cancellationTokenSource.Token));
        }

        private void CommunicationLoop(PacketExchangeManager packetExchangeManager, CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            
            var parser = new PacketBufferParser();
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    //Receiveで受信
                    var length = _socket.Receive(buffer);
                    if (length == 0)
                    {
                        Debug.Log("サーバーから正常に切断されました");
                        break;
                    }
                    
                    //解析をしてunity viewに送る
                    var packets = parser.Parse(buffer, length);
                    foreach (var packet in packets) packetExchangeManager.ExchangeReceivedPacket(packet).Forget();
                }
            }
            catch (SocketException e)
            {
                // ソケットがクローズされた場合は正常終了
                if (!cancellationToken.IsCancellationRequested)
                {
                    Debug.LogError($"ソケットエラー: {e.Message}");
                }
            }
            catch (ObjectDisposedException)
            {
                // ソケットが破棄された場合は正常終了（キャンセル時は何も出力しない）
            }
            catch (Exception e)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Debug.LogError("エラーによりサーバーから切断されました");
                    Debug.LogError($"Message {e.Message} StackTrace {e.StackTrace}");

                    try
                    {
                        var json = MessagePackSerializer.ConvertToJson(buffer);
                        Debug.LogError("受信パケット内容 JSON:" + json);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError("受信パケット内容 JSON:解析に失敗");
                    }
                }
            }
            finally
            {
                Debug.Log("通信ループ終了");
                if (_socket.Connected) _socket.Close();
                InvokeDisconnect().Forget();
            }
        }
        
        private async UniTask InvokeDisconnect()
        {
            await UniTask.SwitchToMainThread();
            _onDisconnect.OnNext(Unit.Default);
        }
        
        public void Send(byte[] data)
        {
            if (_cancellationTokenSource.IsCancellationRequested) return;

            //先頭にパケット長を設定して送信
            var byteCount = ToByteList.Convert(data.Length);
            var newData = new byte[byteCount.Count + data.Length];

            byteCount.CopyTo(newData, 0);
            data.CopyTo(newData, byteCount.Count);

            try
            {
                _socket.Send(newData);
            }
            catch (ObjectDisposedException)
            {
                // ソケットがクローズされている場合は何もしない
            }
        }
        
        public void Close()
        {
            _cancellationTokenSource?.Cancel();
            _socket.Close();
        }
    }
}