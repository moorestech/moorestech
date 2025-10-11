using System;
using System.Net.Sockets;
using System.Threading;
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

        public UserPacketHandler(Socket client, ReceiveQueueProcessor receiveQueueProcessor, SendQueueProcessor sendQueueProcessor)
        {
            _client = client;
            _receiveQueueProcessor = receiveQueueProcessor;
            _sendQueueProcessor = sendQueueProcessor;
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
                Cleanup();
                Debug.Log("切断されました");
            }
            catch (Exception e)
            {
                Cleanup();
                Debug.LogError("moorestech内のロジックによるエラーで切断");
                Debug.LogException(e);
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
            _receiveQueueProcessor.Dispose();
            _sendQueueProcessor.Dispose();
            _client.Close();
        }
    }
}
