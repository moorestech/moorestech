using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using MessagePack;
using Server.Event;
using Server.Protocol;
using UniRx;
using UnityEngine;

namespace Client.Network.API
{
    /// <summary>
    ///     送信されたパケットの応答パケットを<see cref="ServerCommunicator" />から受け取り、呼び出し元に返すクラス
    /// </summary>
    public class PacketExchangeManager
    {
        private readonly PacketSender _packetSender;

        private readonly Dictionary<int, ResponseWaiter> _responseWaiters = new();
        private readonly ConcurrentQueue<byte[]> _receivedPackets = new();
        private readonly Subject<EventMessagePack> _eventPacketSubject = new();

        private int _sequenceId;

        // pushイベントの購読口
        // Subscription point for pushed events
        public IObservable<EventMessagePack> OnEventPacket => _eventPacketSubject;

        public PacketExchangeManager(PacketSender packetSender)
        {
            _packetSender = packetSender;
            TimeOutUpdate().Forget();
            DispatchReceivedPacketsLoop().Forget();

            #region Internal

            async UniTask DispatchReceivedPacketsLoop()
            {
                while (true)
                {
                    // 到着順を保ったままメインスレッドで直列ディスパッチする（順序契約）
                    // Dispatch serially on the main thread preserving arrival order (ordering contract)
                    await UniTask.Yield(PlayerLoopTiming.Update);
                    while (_receivedPackets.TryDequeue(out var data))
                    {
                        // 外部入力デシリアライズ＋購読者呼び出しの境界隔離。1packetの失敗で受信ループを殺さない
                        // External-input boundary: isolate deserialize/subscriber failures so one packet never kills the loop
                        try
                        {
                            DispatchPacket(data);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[PacketExchangeManager] Packet dispatch failed. Continuing.\n{e.Message}\n{e.StackTrace}");
                        }
                    }
                }
            }

            void DispatchPacket(byte[] data)
            {
                var basePacket = MessagePackSerializer.Deserialize<ProtocolMessagePackBase>(data);

                // イベントpushはSequenceId照合ではなくイベントストリームへ流す
                // Route pushed events to the event stream instead of sequence matching
                if (basePacket.Tag == EventStreamMessagePack.ProtocolTag)
                {
                    var eventPacket = MessagePackSerializer.Deserialize<EventStreamMessagePack>(data);
                    _eventPacketSubject.OnNext(eventPacket.Event);
                    return;
                }

                var sequence = basePacket.SequenceId;
                if (!_responseWaiters.ContainsKey(sequence)) return;
                _responseWaiters[sequence].WaitSubject.OnNext((data, PacketWaitCompletionReason.Received));
                _responseWaiters.Remove(sequence);
            }

            #endregion
        }

        private async UniTask TimeOutUpdate()
        {
            while (true)
            {
                // タイムアウト済みの sequenceId を先にスナップショットする
                // Snapshot expired sequenceIds first so we don't mutate while iterating
                var expired = new List<int>();
                foreach (var kv in _responseWaiters)
                {
                    if ((DateTime.Now - kv.Value.SendTime).TotalSeconds >= 10)
                        expired.Add(kv.Key);
                }

                // タイムアウトを明示的に通知して削除する
                // Notify waiters that the packet timed out and remove them
                foreach (var sequenceId in expired)
                {
                    _responseWaiters[sequenceId].WaitSubject.OnNext((null, PacketWaitCompletionReason.Timeout));
                    _responseWaiters.Remove(sequenceId);
                }

                await UniTask.Delay(1000);
            }
        }

        // 受信スレッドから呼ばれる。処理はメインスレッドの直列ループに委譲する
        // Called from the receive thread; processing is deferred to the serial main-thread loop
        public void EnqueueReceivedPacket(byte[] data)
        {
            _receivedPackets.Enqueue(data);
        }

        // 従来互換: 成功時はレスポンス、失敗/タイムアウト時は null を返す
        // Backwards-compatible wrapper: returns the response on success, null otherwise
        [CanBeNull]
        public async UniTask<TResponse> GetPacketResponse<TResponse>(ProtocolMessagePackBase request, CancellationToken ct) where TResponse : ProtocolMessagePackBase
        {
            var (response, _) = await GetPacketResponseWithReason<TResponse>(request, ct);
            return response;
        }

        // 完了理由が必要な呼び出し元向け（タイムアウトを区別したい場合に使う）
        // For callers that need to distinguish between timeout and other failures
        public async UniTask<(TResponse response, PacketWaitCompletionReason reason)> GetPacketResponseWithReason<TResponse>(ProtocolMessagePackBase request, CancellationToken ct) where TResponse : ProtocolMessagePackBase
        {
            SendPacket();

            return await WaitReceive();

            #region Internal

            void SendPacket()
            {
                _sequenceId++;
                request.SequenceId = _sequenceId;
                _packetSender.Send(request);
            }

            async UniTask<(TResponse response, PacketWaitCompletionReason reason)> WaitReceive()
            {
                var responseWaiter = new ResponseWaiter(new Subject<(byte[] data, PacketWaitCompletionReason reason)>());
                _responseWaiters.Add(_sequenceId, responseWaiter);

                var (data, reason) = await responseWaiter.WaitSubject.ToUniTask(true, ct);
                if (reason == PacketWaitCompletionReason.Timeout)
                {
                    // サーバーが 10 秒以内に応答しなかった
                    // The server did not respond within the timeout window
                    Debug.Log($"Packet timed out. Tag:{request.Tag}");
                    return (null, PacketWaitCompletionReason.Timeout);
                }

                try
                {
                    var deserialized = MessagePackSerializer.Deserialize<TResponse>(data);
                    return (deserialized, PacketWaitCompletionReason.Received);
                }
                catch (Exception e)
                {
                    // デシリアライズ失敗は MessagePack 例外でしか検出できないため、ここだけ try-catch を許容する
                    // Catch is localized here because deserialization errors are surfaced only as exceptions from MessagePack
                    Debug.LogError($"デシリアライズに失敗しました。Tag:{request.Tag}\n{e.Message}\n{e.StackTrace}");
                    return (null, PacketWaitCompletionReason.DeserializeFailed);
                }
            }

            #endregion
        }
    }


    public class ResponseWaiter
    {
        public ResponseWaiter(Subject<(byte[] data, PacketWaitCompletionReason reason)> waitSubject)
        {
            WaitSubject = waitSubject;
            SendTime = DateTime.Now;
        }

        public Subject<(byte[] data, PacketWaitCompletionReason reason)> WaitSubject { get; }
        public DateTime SendTime { get; }
    }

    // 応答待ちが完了した理由を明示する
    // Distinguishes how a packet wait completed
    public enum PacketWaitCompletionReason
    {
        Received,
        Timeout,
        DeserializeFailed,
    }
}
