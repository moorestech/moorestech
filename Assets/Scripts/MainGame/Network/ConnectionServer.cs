using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MainGame.Network.Send.SocketUtil;
using MessagePack;
using Server.Util;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Network
{
    public class ConnectionServer : IPostStartable
    {
        public IObservable<(string message,string stackTrace)> OnDisconnect => _onDisconnect;
        private readonly Subject<(string message,string stackTrace)> _onDisconnect = new();

        private readonly AllReceivePacketAnalysisService _allReceivePacketAnalysisService;
        private readonly SocketInstanceCreate _socketInstanceCreate;


        public ConnectionServer(
            AllReceivePacketAnalysisService allReceivePacketAnalysisService,
            SocketInstanceCreate socketInstanceCreate)
        {
            _allReceivePacketAnalysisService = allReceivePacketAnalysisService;
            _socketInstanceCreate = socketInstanceCreate;
        }

        //MonoBehaviourのStartが終わり、全ての初期化が完了した後、サーバーに接続する
        public void PostStart()
        {
            var t = new Thread(Connect);
            t.Start();
        }

        private void Connect()
        {

            Debug.Log("サーバーに接続します");
            //接続を試行する
            try
            {
                _socketInstanceCreate.SocketInstance.Connect(_socketInstanceCreate.GetRemoteEndPoint());
            }
            catch (SocketException e)
            {
                Debug.LogError("サーバーへの接続に失敗しました");
                return;
            }
            
            Debug.Log("サーバーに接続しました");
            var buffer = new byte[4096];
            
            var parser = new PacketBufferParser();
            while (true)
            {
                //Receiveで受信
                var length = _socketInstanceCreate.SocketInstance.Receive(buffer);
                if (length == 0)
                {
                    Debug.LogError("サーバーから切断されました");
                    break;
                }
                    
                //解析をしてunity viewに送る
                var packets = parser.Parse(buffer, length);
                try
                {
                    foreach (var packet in packets)
                    {
                        _allReceivePacketAnalysisService.Analysis(packet);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("エラーによりサーバーから切断されました");
                    if (_socketInstanceCreate.SocketInstance.Connected)
                    {
                        _socketInstanceCreate.SocketInstance.Close();
                    }
                    _onDisconnect.OnNext((e.Message,e.StackTrace));

                    var packetsStr = new StringBuilder();
                    foreach (var @byte in buffer)
                    {
                        packetsStr.Append($"{@byte:X2}" + " ");
                    }


                    try
                    {
                        var json = MessagePackSerializer.ConvertToJson(buffer);
                        Debug.LogError("受信パケット内容 JSON:" + json + " bytes:" +packetsStr);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError("受信パケット内容 JSON:解析に失敗 bytes:" +packetsStr);
                    }

                    throw;
                }
            }
        }
    }
}
