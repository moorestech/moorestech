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
        private int _closeRequested;
        
        private ServerCommunicator(Socket connectedSocket)
        {
            //ソケットを作成
            _socket = connectedSocket;
        }
        
        public IObservable<Unit> OnDisconnect => _onDisconnect;
        
        public static async UniTask<ServerCommunicator> CreateConnectedInstance(ConnectionServerProperties connectionServerProperties, CancellationToken connectCancellation)
        {
            //IPアドレスやポートを設定
            if (!IPAddress.TryParse(connectionServerProperties.IP, out var ipAddress)) throw new ArgumentException("IP解析失敗");
            
            var socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            
            //接続を行う
            socket.Connect(ipAddress, connectionServerProperties.Port);
            
            // 呼び出し側が接続待ちを打ち切ったらソケットも閉じ、待ちとソケットを残さない
            // Close the socket when the caller abandons the wait, leaving neither the wait nor the socket behind
            using var connectCancellationRegistration = connectCancellation.Register(() => socket.Close());
            await UniTask.WaitUntil(() => socket.Connected, PlayerLoopTiming.Update, connectCancellation);
            
            Debug.Log("サーバーに接続しました");
            
            return new ServerCommunicator(socket);
        }
        
        
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
                    foreach (var packet in packets) packetExchangeManager.EnqueueReceivedPacket(packet);
                }
            }
            // 外部Socket受信は明示Closeで実装依存の例外を送出するため、その2種だけを正常終了へ変換する
            // External socket Receive throws implementation-dependent exceptions on explicit Close, so normalize only those two kinds
            catch (ObjectDisposedException) when (Volatile.Read(ref _closeRequested) != 0)
            {
            }
            catch (SocketException) when (Volatile.Read(ref _closeRequested) != 0)
            {
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
                catch (Exception)
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
            // 明示Close公開後の残存frameだけを破棄し、それ以前のSocket障害は例外のまま伝播させる
            // Discard only remaining-frame sends after explicit Close; socket failures before it still propagate
            if (Volatile.Read(ref _closeRequested) != 0) return;

            //先頭にパケット長を設定して送信
            var header = ToByteArray.Convert(data.Length);
            var newData = new byte[header.Length + data.Length];

            header.CopyTo(newData, 0);
            data.CopyTo(newData, header.Length);
            
            _socket.Send(newData);
        }
        
        public void Close()
        {
            // 受信スレッドへ終了意図を先に公開し、CloseによるReceive中断だけを正常終了と判定させる
            // Publish shutdown intent before Close so only the resulting Receive interruption is treated as normal
            if (Interlocked.Exchange(ref _closeRequested, 1) != 0) return;
            _socket.Close();
        }
    }
}
