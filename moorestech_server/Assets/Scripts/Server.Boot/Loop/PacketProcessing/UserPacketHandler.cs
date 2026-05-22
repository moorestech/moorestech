using System;
using System.Net.Sockets;
using System.Threading;
using Game.PlayerConnection;
using MessagePack;
using Server.Protocol;
using Server.Protocol.PacketResponse;
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
        private bool _cleaned;
        private int? _playerId;

        public UserPacketHandler(Socket client, ReceiveQueueProcessor receiveQueueProcessor, SendQueueProcessor sendQueueProcessor, PlayerConnectionRegistry connectionRegistry)
        {
            _client = client;
            _receiveQueueProcessor = receiveQueueProcessor;
            _sendQueueProcessor = sendQueueProcessor;
            _connectionRegistry = connectionRegistry;
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
                TryBindPlayerId(packet);
                _receiveQueueProcessor.EnqueuePacket(packet);
            }

            return false;
        }

        // ハンドシェイクパケットを覗いて、この接続に playerId を紐付ける。
        // Peeks the handshake packet to bind this connection to a playerId.
        private void TryBindPlayerId(byte[] packet)
        {
            if (_playerId.HasValue) return;

            var basePack = MessagePackSerializer.Deserialize<ProtocolMessagePackBase>(packet);
            if (basePack.Tag != InitialHandshakeProtocol.ProtocolTag) return;

            var handshake = MessagePackSerializer.Deserialize<InitialHandshakeProtocol.RequestInitialHandshakeMessagePack>(packet);
            _playerId = handshake.PlayerId;
            _connectionRegistry.Register(handshake.PlayerId);
        }

        private void Cleanup()
        {
            if (_cleaned) return;
            _cleaned = true;

            // 接続終了時、紐付いた playerId を登録解除して切断イベントを発火する。
            // On connection end, unregister the bound playerId and fire the disconnect event.
            if (_playerId.HasValue)
            {
                _connectionRegistry.Unregister(_playerId.Value);
                _playerId = null;
            }

            _receiveQueueProcessor.Dispose();
            _sendQueueProcessor.Dispose();
            _client.Close();
        }
    }
}
