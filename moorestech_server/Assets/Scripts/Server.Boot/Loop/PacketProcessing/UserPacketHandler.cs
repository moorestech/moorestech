using System;
using System.Net.Sockets;
using System.Threading;
using Game.PlayerConnection;
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
        private bool _cleaned;

        public UserPacketHandler(Socket client, ReceiveQueueProcessor receiveQueueProcessor, SendQueueProcessor sendQueueProcessor, PlayerConnectionRegistry connectionRegistry, EventProtocolProvider eventProtocolProvider, PacketResponseContext packetResponseContext)
        {
            _client = client;
            _receiveQueueProcessor = receiveQueueProcessor;
            _sendQueueProcessor = sendQueueProcessor;
            _connectionRegistry = connectionRegistry;
            _eventProtocolProvider = eventProtocolProvider;
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

            // close確定を先に記録し、以後のhandshakeバインドを失敗させる（handshake中切断のsink残留防止）
            // Mark closed first so a concurrent handshake bind fails; prevents sink leaks on mid-handshake disconnect
            var playerId = _packetResponseContext.MarkClosedAndGetPlayerId();
            if (playerId.HasValue)
            {
                // この接続のsinkだけを解除し、切断イベントを発火する
                // Unregister only this connection's sink, then fire the disconnect event
                _eventProtocolProvider.UnregisterPlayer(playerId.Value, _packetResponseContext.EventSink);
                _connectionRegistry.Unregister(playerId.Value);
            }

            _receiveQueueProcessor.Dispose();
            _sendQueueProcessor.Dispose();
            _client.Close();
        }
    }
}
