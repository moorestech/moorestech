using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MainGame.Model.Network;
using MainGame.Network.Send.SocketUtil;
using MainGame.Network.Util;
using UnityEngine;
using VContainer.Unity;

namespace MainGame.Network
{
    public class ConnectionServer : IPostStartable
    {
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
                _socketInstanceCreate.GetSocket().Connect(_socketInstanceCreate.GetRemoteEndPoint());
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
                var length = _socketInstanceCreate.GetSocket().Receive(buffer);
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
                    if (_socketInstanceCreate.GetSocket().Connected)
                    {
                        _socketInstanceCreate.GetSocket().Close();
                    }

                    var packetsStr = new StringBuilder();
                    foreach (var @byte in buffer)
                    {
                        packetsStr.Append(@byte + " ");
                    }
                    Debug.LogError("受信パケット内容：" + packetsStr);
                    throw new Exception("受信パケット解析失敗", e);
                }
            }
        }
    }
}
