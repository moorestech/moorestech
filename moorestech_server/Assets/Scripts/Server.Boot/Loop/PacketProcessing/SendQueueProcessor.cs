using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using MessagePack;
using Server.Event;
using Server.Protocol;
using Server.Util;
using UnityEngine;

namespace Server.Boot.Loop.PacketProcessing
{
    /// <summary>
    /// 送信キュープロセッサ
    /// メインスレッドから送信データをEnqueueし、送信スレッドがEnqueueされ次第即座にSocketへ送信する
    /// BlockingCollectionにより待機中はCPUを消費せず、Enqueueで即座に送信スレッドが起きる
    /// </summary>
    public class SendQueueProcessor : IPlayerEventSink
    {
        private readonly Socket _client;
        private readonly BlockingCollection<byte[]> _sendQueue = new();
        private readonly Thread _sendThread;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        // サーバー停止とUnity停止の意思。受信側と同じく、終了中に相手が先に消えた送信失敗を内部エラーと区別するために持つ
        // Server-stop and Unity-exit intents; like the receive side, they tell a send failure from a peer that vanished during shutdown apart from an internal error
        private readonly CancellationToken _serverCancellation;
        private readonly CancellationToken _shutdownNotice;

        public SendQueueProcessor(Socket client, CancellationToken serverCancellation, CancellationToken shutdownNotice)
        {
            _client = client;
            _serverCancellation = serverCancellation;
            _shutdownNotice = shutdownNotice;

            // 送信スレッドを起動
            _sendThread = new Thread(SendThreadLoop);
            _sendThread.Name = "[moorestech] パケット送信スレッド";
            _sendThread.Start();
        }

        public void EnqueueMessage(byte[] body)
        {
            // Dispose後は積まない。消費者のいないキューが無限成長するのを防ぐ
            // Reject enqueue after dispose so a consumerless queue never grows unboundedly
            if (_cancellationTokenSource.IsCancellationRequested) return;

            var header = ToByteArray.Convert(body.Length);
            var sendData = new byte[header.Length + body.Length];
            header.CopyTo(sendData, 0);
            body.CopyTo(sendData, header.Length);
            _sendQueue.Add(sendData);
        }

        // イベント経由のデータ送信
        // Event-driven data sending
        public void EnqueueEvent(EventMessagePack eventMessagePack)
        {
            var body = MessagePackSerializer.Serialize(new EventStreamMessagePack(eventMessagePack));
            EnqueueMessage(body);
        }

        private void SendThreadLoop()
        {
            // 外部境界（Socket送信）の隔離try-catch。キャンセル時はTakeのOperationCanceledExceptionで抜ける
            // Boundary try-catch isolating socket sends; cancellation exits via Take's OperationCanceledException
            try
            {
                var token = _cancellationTokenSource.Token;
                while (!token.IsCancellationRequested)
                {
                    // データが積まれるまでブロックし、積まれた瞬間に送信
                    // Block until data is enqueued, then send immediately
                    var dataToSend = _sendQueue.Take(token);
                    SendAll(dataToSend);
                }
            }
            // 外部境界（ネットワーク送信）: 終了中に相手が先に切れた送信失敗は想定内。受信側（UserPacketHandler）と同じ条件で分類し、終了意思の無い失敗は従来どおりErrorに残す
            // External boundary (network send): a send failing because the peer left during shutdown is expected; classify it on the receive side's (UserPacketHandler) condition and keep failures without shutdown intent as errors
            catch (SocketException exception) when (!_cancellationTokenSource.IsCancellationRequested && (_serverCancellation.IsCancellationRequested || _shutdownNotice.IsCancellationRequested))
            {
                Debug.Log($"サーバー終了中のSocket送信を終了しました: {exception.SocketErrorCode}");
                ShutdownForReceive();
            }
            catch (Exception e)
            {
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    Debug.LogError("送信スレッドでエラーが発生しました");
                    Debug.LogException(e);

                    // 送信が壊れた接続はもう相手へ届かない。受信側のReceiveも抜けさせて通常の切断処理へ流す
                    // A connection whose send broke can no longer reach the peer, so unblock its Receive and let the normal disconnect path run
                    ShutdownForReceive();
                }
            }

            #region Internal

            // ソケット操作は外部境界。既に閉じている接続への操作は例外になるため隔離し、理由だけ残して進む
            // Socket operations are an external boundary; acting on an already-closed connection throws, so isolate it and move on with the reason logged
            // Closeはここで呼ばない。受信スレッドのReceiveがObjectDisposedExceptionで抜けると相手方の障害が内部バグとして記録される
            // Close is not called here; a Receive failing with ObjectDisposedException would log the peer's fault as an internal bug
            void ShutdownForReceive()
            {
                try
                {
                    _client.Shutdown(SocketShutdown.Both);
                }
                catch (Exception shutdownException)
                {
                    Debug.LogWarning($"送信失敗後のソケット終了に失敗しました: {shutdownException.Message}");
                }
            }

            #endregion
        }

        private void SendAll(byte[] data)
        {
            var offset = 0;
            var remaining = data.Length;

            while (remaining > 0)
            {
                var sent = _client.Send(data, offset, remaining, SocketFlags.None);
                offset += sent;
                remaining -= sent;
            }
        }

        public void Dispose()
        {
            if (_cancellationTokenSource.IsCancellationRequested) return;

            _cancellationTokenSource.Cancel();

            // 送信スレッドの終了を待つ
            if (_sendThread != null && _sendThread.IsAlive)
            {
                _sendThread.Join(TimeSpan.FromSeconds(5));
            }

            _cancellationTokenSource.Dispose();
        }
    }
}
