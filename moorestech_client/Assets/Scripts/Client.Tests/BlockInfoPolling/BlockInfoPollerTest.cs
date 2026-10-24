using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading;
using Client.Game.InGame.BlockInfoPolling;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BlockInfoPolling
{
    /// <summary>
    ///     未取得→取得済み→通信失敗（保持）→消失（停止）の遷移と、通知が取得済み・消失のときだけ出ることを固定する
    ///     Pins the not fetched -> fetched -> transport failure (kept) -> vanished (stopped) transitions and that only fetch and vanish notify
    /// </summary>
    public class BlockInfoPollerTest
    {
        [UnityTest]
        public IEnumerator FetchedKeepsOnTransportFailureAndClearsOnVanishTest() => UniTask.ToCoroutine(async () =>
        {
            var source = new ScriptedPollSource();
            var poller = new BlockInfoPoller<ScriptedResponse, string>(source, "ScriptedInfo", TimeSpan.FromMilliseconds(10));
            var updates = 0;
            poller.OnBlockInfoSnapshotUpdated.Subscribe(_ => updates++);

            // 開始直後は未取得で値を持たない
            // Right after starting it is not fetched and holds no value
            poller.Start(new BlockInstanceId(1));
            Assert.AreEqual(BlockInfoFetchState.NotFetched, poller.State);
            Assert.IsFalse(poller.TryGetSnapshot(out _));

            // 応答が来ると取得済みになり値を持つ
            // A response makes it fetched with a value
            await source.Respond(new ScriptedResponse("room-a", true));
            Assert.AreEqual(BlockInfoFetchState.Fetched, poller.State);
            Assert.IsTrue(poller.TryGetSnapshot(out var fetched));
            Assert.AreEqual("room-a", fetched);

            // 通信失敗(null)は状態と前回値を保ち、通知しない
            // A transport failure (null) keeps the state and last value and does not notify
            LogAssert.Expect(LogType.Warning, new Regex(@"\[BlockInfoPoller\] ScriptedInfo failed for block"));
            await source.Respond(null);
            Assert.AreEqual(BlockInfoFetchState.Fetched, poller.State);
            Assert.IsTrue(poller.TryGetSnapshot(out var kept));
            Assert.AreEqual("room-a", kept);

            // ブロック消失は値を捨てて消失になる
            // A vanished block drops the value and turns vanished
            LogAssert.Expect(LogType.Warning, new Regex(@"\[BlockInfoPoller\] ScriptedInfo found no block"));
            await source.Respond(new ScriptedResponse(null, false));
            Assert.AreEqual(BlockInfoFetchState.Vanished, poller.State);
            Assert.IsFalse(poller.TryGetSnapshot(out _));
            Assert.AreEqual(2, updates);

            // 消失したら問い合わせを止めるので、間隔の10倍待っても警告も通知も繰り返さない
            // Vanishing stops the requests, so even ten intervals later neither the warning nor the notification repeats
            var requestsAtVanish = source.RequestCount;
            await UniTask.Delay(TimeSpan.FromMilliseconds(100));
            Assert.AreEqual(requestsAtVanish, source.RequestCount);
            Assert.AreEqual(BlockInfoFetchState.Vanished, poller.State);
            Assert.AreEqual(2, updates);

            // 止めると未取得に戻る
            // Stopping returns it to not fetched
            poller.Stop();
            Assert.AreEqual(BlockInfoFetchState.NotFetched, poller.State);
        });

        private sealed class ScriptedResponse
        {
            public readonly string Snapshot;
            public readonly bool BlockExists;

            public ScriptedResponse(string snapshot, bool blockExists)
            {
                Snapshot = snapshot;
                BlockExists = blockExists;
            }
        }

        // 問い合わせごとに保留の完了元を作り、テストが Respond で1件ずつ返す
        // Each request creates a pending completion source that the test resolves one by one with Respond
        private sealed class ScriptedPollSource : IBlockInfoPollSource<ScriptedResponse, string>
        {
            private UniTaskCompletionSource<ScriptedResponse> _pending;

            public int RequestCount { get; private set; }

            public UniTask<ScriptedResponse> Request(BlockInstanceId blockInstanceId, CancellationToken ct)
            {
                RequestCount++;
                _pending = new UniTaskCompletionSource<ScriptedResponse>();
                return _pending.Task;
            }

            public bool TryGetSnapshot(ScriptedResponse response, out string snapshot)
            {
                snapshot = response.Snapshot;
                return response.BlockExists;
            }

            // 次の問い合わせが来るまで待ってから応答を返し、ポーラーの反映を1フレーム待つ
            // Wait for the next request, resolve it, then wait one frame for the poller to apply it
            public async UniTask Respond(ScriptedResponse response)
            {
                await UniTask.WaitUntil(() => _pending != null);
                var pending = _pending;
                _pending = null;
                pending.TrySetResult(response);
                await UniTask.Yield();
            }
        }
    }
}
