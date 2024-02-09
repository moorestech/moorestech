using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Client.Network.NewApi;
using Cysharp.Threading.Tasks;
using MainGame.Network.Send.SocketUtil;
using MessagePack;
using Server.Util;
using UniRx;
using UnityEngine;

namespace MainGame.Network
{
    public class ConnectionServer
    {
        private readonly ServerConnector _serverConnector;
        private readonly Subject<Unit> _onDisconnect = new();
        private readonly SocketInstanceCreate _socketInstanceCreate;


        public ConnectionServer(
            ServerConnector serverConnector,
            SocketInstanceCreate socketInstanceCreate)
        {
            _serverConnector = serverConnector;
            _socketInstanceCreate = socketInstanceCreate;

            Task.Run(Connect);
        }

        public IObservable<Unit> OnDisconnect => _onDisconnect;


        private async Task Connect()
        {
            //サーバーに接続する前に全体の処理を待つ
            await Task.Delay(2000);

            Debug.Log("サーバーに接続します");
            //接続を試行する
            try
            {
                _socketInstanceCreate.SocketInstance.Connect(_socketInstanceCreate.GetRemoteEndPoint());
            }
            catch (SocketException e)
            {
                Debug.LogError("サーバーへの接続に失敗しました");
                Debug.LogError($"Message {e.Message} StackTrace {e.StackTrace}");
                return;
            }

            Debug.Log("サーバーに接続しました");
            var buffer = new byte[4096];

            var parser = new PacketBufferParser();
            try
            {
                while (true)
                {
                    //Receiveで受信
                    var length = _socketInstanceCreate.SocketInstance.Receive(buffer);
                    if (length == 0)
                    {
                        Debug.LogError("ストリームがゼロによる切断");
                        break;
                    }

                    //解析をしてunity viewに送る
                    var packets = parser.Parse(buffer, length);
                    foreach (var packet in packets) _serverConnector.ReceiveData(packet).Forget();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("エラーによりサーバーから切断されました");
                Debug.LogError($"Message {e.Message} StackTrace {e.StackTrace}");
                if (_socketInstanceCreate.SocketInstance.Connected) _socketInstanceCreate.SocketInstance.Close();

                var packetsStr = new StringBuilder();
                foreach (var @byte in buffer) packetsStr.Append($"{@byte:X2}" + " ");


                try
                {
                    var json = MessagePackSerializer.ConvertToJson(buffer);
                    Debug.LogError("受信パケット内容 JSON:" + json + " bytes:" + packetsStr);
                }
                catch (Exception exception)
                {
                    Debug.LogError("受信パケット内容 JSON:解析に失敗 bytes:" + packetsStr);
                }

                throw;
            }
            finally
            {
                Debug.Log("通信ループ終了");
                InvokeDisconnect().Forget();
            }
        }

        private async UniTask InvokeDisconnect()
        {
            await UniTask.SwitchToMainThread();
            _onDisconnect.OnNext(Unit.Default);
        }
    }
}