using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace Server.Boot.Loop.PacketProcessing
{
    /// <summary>
    /// 送信キュープロセッサ
    /// メインスレッドから送信データをEnqueueし、送信スレッドで50msごとにSocketに送信する
    /// ConcurrentQueueを使用してlock-freeで高速処理
    /// </summary>
    public class SendQueueProcessor
    {
        private readonly Socket _client;
        private readonly ConcurrentQueue<byte[]> _sendQueue = new();
        private readonly Thread _sendThread;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        private const int SendIntervalMs = 50;

        public SendQueueProcessor(Socket client)
        {
            _client = client;

            // 送信スレッドを起動
            _sendThread = new Thread(SendThreadLoop);
            _sendThread.Name = "[moorestech] パケット送信スレッド";
            _sendThread.Start();
        }

        public void EnqueueSendData(byte[] data)
        {
            _sendQueue.Enqueue(data);
        }

        private void SendThreadLoop()
        {
            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // 50msごとにキューをチェックして送信
                    Thread.Sleep(SendIntervalMs);

                    // キューにあるすべてのデータを送信
                    while (_sendQueue.TryDequeue(out var dataToSend))
                    {
                        SendAll(dataToSend);
                    }
                }
            }
            catch (Exception e)
            {
                if (!_cancellationTokenSource.Token.IsCancellationRequested)
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
