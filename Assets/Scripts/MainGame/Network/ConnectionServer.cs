using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MainGame.Model.Network;
using MainGame.Network.Send.SocketUtil;
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
            byte[] bytes = new byte[4096];
            while (true)
            {
                try
                {
                    //Receiveで受信
                    var len = _socketInstanceCreate.GetSocket().Receive(bytes);
                    if (len == 0)
                    {
                        Debug.LogError("サーバーから切断されました");
                        break;
                    }

                    try
                    {
                        //解析を行う
                        _allReceivePacketAnalysisService.Analysis(bytes);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("受信パケット解析失敗：" + e);
                        var packets = new StringBuilder();
                        foreach (var @byte in bytes)
                        {
                            packets.Append(@bytes);
                        }
                        Debug.LogError("受信パケット内容：" + packets);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("エラーによりサーバーから説残されました "+e);
                    
                    if (_socketInstanceCreate.GetSocket().Connected)
                    {
                        _socketInstanceCreate.GetSocket().Close();
                    }
                    return;
                }
            }
        }
    }
}
