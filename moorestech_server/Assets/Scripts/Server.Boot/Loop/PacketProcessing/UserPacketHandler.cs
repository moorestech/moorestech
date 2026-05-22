using System;
using System.Net.Sockets;
using System.Threading;
using Game.PlayerConnection;
using Server.Protocol;
using Server.Util;
using UnityEngine;

namespace Server.Boot.Loop.PacketProcessing
{
    /// <summary>
    /// ユーザーパケットハンドラー
    /// 受信スレッドでSocket.Receive()を実行し、パケットをReceiveQueueProcessorにEnqueue
    /// </summary>
    public class UserPacketHandler
    {
        private readonly Socket _client;
        private readonly ReceiveQueueProcessor _receiveQueueProcessor;
        private readonly SendQueueProcessor _sendQueueProcessor;
        private readonly PlayerConnectionRegistry _connectionRegistry;
        private readonly PacketResponseContext _packetResponseContext;
        private bool _cleaned;

        public UserPacketHandler(Socket client, ReceiveQueueProcessor receiveQueueProcessor, SendQueueProcessor sendQueueProcessor, PlayerConnectionRegistry connectionRegistry, PacketResponseContext packetResponseContext)
        {
            _client = client;
            _receiveQueueProcessor = receiveQueueProcessor;
            _sendQueueProcessor = sendQueueProcessor;
            _connectionRegistry = connectionRegistry;
            _packetResponseContext = packetResponseContext;
        }

        public void StartListen(CancellationToken token)
        {
            var buffer = new byte[4096];

            try
            {
                var parser = new PacketBufferParser();
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    var error = ReceiveProcess(parser, buffer);
                    if (error)
                    {
                        Debug.Log("切断されました");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("切断されました");
            }
            catch (Exception e)
            {
                Debug.LogError("moorestech内のロジックによるエラーで切断");
                Debug.LogException(e);
            }
            finally
            {
                Cleanup();
            }
        }

        private bool ReceiveProcess(PacketBufferParser parser, byte[] buffer)
        {
            var length = _client.Receive(buffer);
            if (length == 0) return true;

            // 受信データをパケットに分割
            var packets = parser.Parse(buffer, length);

            // パケット処理はメインスレッドに委譲
            foreach (var packet in packets)
            {
                _receiveQueueProcessor.EnqueuePacket(packet);
            }

            return false;
        }

        private void Cleanup()
        {
            if (_cleaned) return;
            _cleaned = true;

            // 接続終了時、紐付いた playerId を登録解除して切断イベントを発火する。
            // On connection end, unregister the bound playerId and fire the disconnect event.
            if (_packetResponseContext.PlayerId.HasValue)
            {
                _connectionRegistry.Unregister(_packetResponseContext.PlayerId.Value);
            }

            _receiveQueueProcessor.Dispose();
            _sendQueueProcessor.Dispose();
            _client.Close();
        }
    }
}
