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

        public SendQueueProcessor(Socket client)
        {
            _client = client;

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
            catch (Exception e)
            {
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    Debug.LogError("送信スレッドでエラーが発生しました");
                    Debug.LogException(e);
                }
            }
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
