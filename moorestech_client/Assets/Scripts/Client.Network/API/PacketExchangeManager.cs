using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using MessagePack;
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

        private int _sequenceId;

        public PacketExchangeManager(PacketSender packetSender)
        {
            _packetSender = packetSender;
            TimeOutUpdate().Forget();
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

        public async UniTask ExchangeReceivedPacket(byte[] data)
        {
            var response = MessagePackSerializer.Deserialize<ProtocolMessagePackBase>(data);
            var sequence = response.SequenceId;

            await UniTask.SwitchToMainThread();

            if (!_responseWaiters.ContainsKey(sequence)) return;
            // 正常に応答が届いたことを通知する
            // Notify waiter that a valid response has arrived
            _responseWaiters[sequence].WaitSubject.OnNext((data, PacketWaitCompletionReason.Received));
            _responseWaiters.Remove(sequence);
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
