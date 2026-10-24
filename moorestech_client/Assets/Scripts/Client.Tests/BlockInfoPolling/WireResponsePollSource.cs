using System;
using System.Threading;
using Client.Game.InGame.BlockInfoPolling;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using MessagePack;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BlockInfoPolling
{
    // 通信だけを制御し、復号とビュー検証は本番経路を通す
    // Control only transport while using real deserialization and view validation
    internal sealed class WireResponsePollSource<TResponse, TSnapshot> : IBlockInfoPollSource<TResponse, TSnapshot> where TResponse : class
    {
        private readonly IBlockInfoPollSource<TResponse, TSnapshot> _decoder;
        private UniTaskCompletionSource<TResponse> _pending;

        public WireResponsePollSource(IBlockInfoPollSource<TResponse, TSnapshot> decoder)
        {
            _decoder = decoder;
        }

        public UniTask<TResponse> Request(BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            _pending = new UniTaskCompletionSource<TResponse>();
            return _pending.Task.AttachExternalCancellation(ct);
        }

        public bool TryGetSnapshot(TResponse response, out TSnapshot snapshot)
        {
            return _decoder.TryGetSnapshot(response, out snapshot);
        }

        public async UniTask Respond(TResponse response)
        {
            // 往復した応答を待機中の問い合わせへ渡す
            // Deliver a wire-round-tripped response to the pending request
            await UniTask.WaitUntil(() => _pending != null);
            var pending = _pending;
            _pending = null;
            pending.TrySetResult(MessagePackSerializer.Deserialize<TResponse>(MessagePackSerializer.Serialize(response)));
            await UniTask.Yield();
        }

        public async UniTask AssertInvalidResponseClearsSnapshot(TResponse valid, TResponse invalid)
        {
            var poller = new BlockInfoPoller<TResponse, TSnapshot>(this, "WireResponse", TimeSpan.FromMilliseconds(1));
            var notifications = 0;
            using var subscription = poller.OnBlockInfoSnapshotUpdated.Subscribe(_ => notifications++);
            poller.Start(new BlockInstanceId(1));
            await Respond(valid);
            Assert.AreEqual(BlockInfoFetchState.Fetched, poller.State);
            Assert.IsTrue(poller.TryGetSnapshot(out _));
            Assert.AreEqual(1, notifications);

            // 不正応答を実デコーダへ流し、古い値の破棄と通知を固定する
            // Feed the real decoder an invalid response and pin stale-value removal and notification
            LogAssert.Expect(LogType.Warning, "Unreadable response kind: 999");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[BlockInfoPoller\] WireResponse found no block"));
            await Respond(invalid);
            Assert.AreEqual(BlockInfoFetchState.Vanished, poller.State);
            Assert.IsFalse(poller.TryGetSnapshot(out var discarded));
            Assert.IsNull(discarded);
            Assert.AreEqual(2, notifications);
            poller.Stop();
        }
    }
}
