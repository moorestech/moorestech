using System;
using System.Net.Sockets;
using System.Threading;
using Game.PlayerConnection;
using Game.SaveLoad.Snapshot;
using Server.Event;
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
        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly PacketResponseContext _packetResponseContext;
        private readonly ReceivedPacketLog _receivedPacketLog;
        private readonly TickEndPacketQueue _tickEndPacketQueue;
        private bool _cleaned;

        public UserPacketHandler(Socket client, ReceiveQueueProcessor receiveQueueProcessor, SendQueueProcessor sendQueueProcessor, PlayerConnectionRegistry connectionRegistry, EventProtocolProvider eventProtocolProvider, PacketResponseContext packetResponseContext, ReceivedPacketLog receivedPacketLog, TickEndPacketQueue tickEndPacketQueue)
        {
            _client = client;
            _receiveQueueProcessor = receiveQueueProcessor;
            _sendQueueProcessor = sendQueueProcessor;
            _connectionRegistry = connectionRegistry;
            _eventProtocolProvider = eventProtocolProvider;
            _packetResponseContext = packetResponseContext;
            _receivedPacketLog = receivedPacketLog;
            _tickEndPacketQueue = tickEndPacketQueue;
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

            // 接続集合からの解除は即時、記録とイベント宛先の解除はFIFOで。順序はSchedule側が所有する
            // Immediate removal from the connection set, FIFO for the record and event-sink removal; Schedule owns that order
            ConnectionDisconnectEntry.Schedule(_packetResponseContext, _receiveQueueProcessor,
                _tickEndPacketQueue, _connectionRegistry, _eventProtocolProvider, _receivedPacketLog);
            _sendQueueProcessor.Dispose();
            _client.Close();
        }
    }
}
