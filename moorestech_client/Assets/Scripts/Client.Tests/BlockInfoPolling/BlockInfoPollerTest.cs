using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading;
using Client.Game.InGame.BlockInfoPolling;
using Client.Game.InGame.CleanRoom;
using Client.WebUiHost.Game.Topics.BlockDetail.MachineInput;
using Server.Protocol.PacketResponse;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BlockInfoPolling
{
    /// <summary>
    ///     未取得→取得済み→通信失敗（値は保つが最新ではない）→読めない応答（破棄・再試行）→非対応（破棄・再試行）→回復→消失（停止）の遷移と、応答ごとの通知を固定する
    ///     Pins not fetched -> fetched -> transport failure (value kept but not latest) -> unreadable (dropped, retried) -> unsupported (dropped, retried) -> recovery -> vanished (stopped), notifying on every response
    /// </summary>
    public class BlockInfoPollerTest
    {
        [UnityTest]
        public IEnumerator ReadResultsDriveFetchStateAndOnlyVanishStopsTest() => UniTask.ToCoroutine(async () =>
        {
            var source = new ScriptedPollSource();
            var poller = new BlockInfoPoller<ScriptedResponse, string>(source, "ScriptedInfo", TimeSpan.FromMilliseconds(10));
            var updates = 0;
            poller.OnBlockInfoSnapshotUpdated.Subscribe(_ => updates++);

            // 開始直後は未取得で値を持たない
            // Right after starting it is not fetched and holds no value
            poller.Start(new BlockInstanceId(1));
            Assert.AreEqual(BlockInfoFetchState.NotFetched, poller.GetLatest(out _));
            Assert.IsFalse(poller.TryGetSnapshot(out _));

            // 応答が来ると取得済みになり値を持つ
            // A response makes it fetched with a value
            await source.Respond(new ScriptedResponse("room-a", BlockInfoReadResult.Success));
            Assert.AreEqual(BlockInfoFetchState.Fetched, poller.GetLatest(out var latest));
            Assert.AreEqual("room-a", latest);

            // 通信失敗(null)は最後の値を保つが、最新の値としては渡さず通知する
            // A transport failure (null) keeps the last value but hands out no latest value, and notifies
            LogAssert.Expect(LogType.Warning, new Regex(@"\[BlockInfoPoller\] ScriptedInfo failed for block"));
            await source.Respond(null);
            Assert.AreEqual(BlockInfoFetchState.TransportFailed, poller.GetLatest(out var stale));
            Assert.IsNull(stale);
            Assert.IsTrue(poller.TryGetSnapshot(out var kept));
            Assert.AreEqual("room-a", kept);

            // 読めない応答と非対応の対象は値を捨て、問い合わせは続ける
            // An unreadable response and an unsupported target drop the value while polling continues
            LogAssert.Expect(LogType.Warning, new Regex(@"\[BlockInfoPoller\] ScriptedInfo got an unreadable response"));
            await source.Respond(new ScriptedResponse("broken", BlockInfoReadResult.Unreadable));
            Assert.AreEqual(BlockInfoFetchState.Unreadable, poller.State);
            Assert.IsFalse(poller.TryGetSnapshot(out _));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[BlockInfoPoller\] ScriptedInfo does not support block"));
            await source.Respond(new ScriptedResponse("chest", BlockInfoReadResult.UnsupportedTarget));
            Assert.AreEqual(BlockInfoFetchState.UnsupportedTarget, poller.State);
            Assert.IsFalse(poller.TryGetSnapshot(out _));

            // 再試行で正常な応答が来れば取得済みへ戻る
            // A valid response on retry brings it back to fetched
            await source.Respond(new ScriptedResponse("room-b", BlockInfoReadResult.Success));
            Assert.AreEqual(BlockInfoFetchState.Fetched, poller.GetLatest(out var recovered));
            Assert.AreEqual("room-b", recovered);

            // ブロック消失は値を捨てて消失になる
            // A vanished block drops the value and turns vanished
            LogAssert.Expect(LogType.Warning, new Regex(@"\[BlockInfoPoller\] ScriptedInfo found no block"));
            await source.Respond(new ScriptedResponse(null, BlockInfoReadResult.BlockNotFound));
            Assert.AreEqual(BlockInfoFetchState.Vanished, poller.State);
            Assert.IsFalse(poller.TryGetSnapshot(out _));
            Assert.AreEqual(6, updates);

            // 消失したら問い合わせを止めるので、間隔の10倍待っても警告も通知も繰り返さない
            // Vanishing stops the requests, so even ten intervals later neither the warning nor the notification repeats
            var requestsAtVanish = source.RequestCount;
            await UniTask.Delay(TimeSpan.FromMilliseconds(100));
            Assert.AreEqual(requestsAtVanish, source.RequestCount);
            Assert.AreEqual(BlockInfoFetchState.Vanished, poller.State);
            Assert.AreEqual(6, updates);

            // 止めると未取得に戻る
            // Stopping returns it to not fetched
            poller.Stop();
            Assert.AreEqual(BlockInfoFetchState.NotFetched, poller.State);
        });

        [UnityTest]
        public IEnumerator InvalidRoomWireResponseDropsAndRecoversTest() => UniTask.ToCoroutine(async () =>
        {
            var valid = GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack.CreateNotEnclosed(CleanRoomLeakMessagePack.CreateNoEnclosure());
            var invalid = GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack.CreateBlockNotFound();
            invalid.Kind = (CleanRoomInfoResponseKind)999;
            var source = new WireResponsePollSource<GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack, CleanRoomInfoView>(new CleanRoomInfoPollSource());
            await source.AssertInvalidResponseDropsAndRecovers(valid, invalid);
        });

        [UnityTest]
        public IEnumerator InvalidYieldWireResponseDropsAndRecoversTest() => UniTask.ToCoroutine(async () =>
        {
            var valid = GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateWaferGrade(3, 0.5);
            var invalid = GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateNoRole();
            invalid.Kind = (CleanRoomYieldResponseKind)999;
            var source = new WireResponsePollSource<GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack, CleanRoomYieldView>(new CleanRoomYieldInfoPollSource());
            await source.AssertInvalidResponseDropsAndRecovers(valid, invalid);
        });

        [UnityTest]
        public IEnumerator InvalidStallWireResponseDropsAndRecoversTest() => UniTask.ToCoroutine(async () =>
        {
            var valid = GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack.CreateStalled(MachineInputStallReasonKind.AboveCraftCap, 12);
            var invalid = GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack.CreateNotStalled();
            invalid.Kind = (MachineInputStallResponseKind)999;
            var source = new WireResponsePollSource<GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack, MachineInputStallView>(new MachineInputStallPollSource());
            await source.AssertInvalidResponseDropsAndRecovers(valid, invalid);
        });

        private sealed class ScriptedResponse
        {
            public readonly string Snapshot;
            public readonly BlockInfoReadResult ReadResult;

            public ScriptedResponse(string snapshot, BlockInfoReadResult readResult)
            {
                Snapshot = snapshot;
                ReadResult = readResult;
            }
        }

        // - 問い合わせごとに完了元を生成
        // - テストが一件ずつ応答
        // - Creates a completion source per request
        // - the test responds one at a time
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

            public BlockInfoReadResult TryRead(ScriptedResponse response, out string snapshot)
            {
                snapshot = response.ReadResult == BlockInfoReadResult.Success ? response.Snapshot : null;
                return response.ReadResult;
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
