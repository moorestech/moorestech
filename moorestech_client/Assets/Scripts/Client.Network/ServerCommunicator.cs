using System;
using System.Net;
using System.Net.Sockets;
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
        
        private ServerCommunicator(Socket connectedSocket)
        {
            //ソケットを作成
            _socket = connectedSocket;
        }
        
        public IObservable<Unit> OnDisconnect => _onDisconnect;
        
        public static async UniTask<ServerCommunicator> CreateConnectedInstance(ConnectionServerProperties connectionServerProperties)
        {
            //IPアドレスやポートを設定
            if (!IPAddress.TryParse(connectionServerProperties.IP, out var ipAddress)) throw new ArgumentException("IP parsing failed");
            
            var socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            
            //接続を行う
            socket.Connect(ipAddress, connectionServerProperties.Port);
            
            // 接続に10秒かかったらエラーを出す
            await UniTask.WaitUntil(() => socket.Connected).Timeout(TimeSpan.FromSeconds(10));
            
            Debug.Log("Connected to server");
            
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
                        Debug.LogError("Disconnected due to zero-length stream");
                        break;
                    }
                    
                    //解析をしてunity viewに送る
                    var packets = parser.Parse(buffer, length);
                    foreach (var packet in packets) packetExchangeManager.ExchangeReceivedPacket(packet).Forget();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Disconnected from server due to error");
                Debug.LogError($"Message {e.Message} StackTrace {e.StackTrace}");
                if (_socket.Connected) _socket.Close();
                
                try
                {
                    var json = MessagePackSerializer.ConvertToJson(buffer);
                    Debug.LogError("Received packet content JSON:" + json);
                }
                catch (Exception exception)
                {
                    Debug.LogError("Received packet content JSON: parsing failed");
                }
                
                throw;
            }
            finally
            {
                Debug.Log("Communication loop ended");
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