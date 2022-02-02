using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MainGame.Network.Send;
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

        public void Connect()
        {

            Debug.Log("サーバーに接続します");
            //接続を試行する
            _socketInstanceCreate.GetSocket().Connect(_socketInstanceCreate.GetRemoteEndPoint());
            
            Debug.Log("サーバーに接続しました");
            byte[] bytes = new byte[4096];
            while (true)
            {
                //Receiveで受信
                var len = _socketInstanceCreate.GetSocket().Receive(bytes);
                if (len == 0)
                {
                    Debug.Log("サーバーから切断されました");
                    break;
                }
                //解析を行う
                _allReceivePacketAnalysisService.Analysis(bytes);
            }
        }
    }
}
