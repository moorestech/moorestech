using System;
using System.Reflection;
using System.Text.RegularExpressions;
using Client.Game.InGame.Train.Network;
using Cysharp.Threading.Tasks;
using MessagePack;
using NUnit.Framework;
using Server.Event.EventReceive;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests
{
    public class TrainFullSnapshotFailurePropagationTest
    {
        private const string RailGraphHandlerName = "HandleRailGraphFullSnapshot";
        private const string TrainUnitHandlerName = "HandleTrainUnitFullSnapshot";

        // MessagePackが決して出力しないバイトなのでデシリアライズ段で必ず失敗する
        // A byte MessagePack never emits, so deserialization is guaranteed to fail
        private static readonly byte[] UndeserializablePayload = { 0xC1 };

        [Test]
        public void railGraphのデシリアライズ失敗は待機タスクへ例外として届く()
        {
            AssertFailureReachesWaitingTask<MessagePackSerializationException>(RailGraphHandlerName, UndeserializablePayload);
        }

        [Test]
        public void trainUnitのデシリアライズ失敗は待機タスクへ例外として届く()
        {
            AssertFailureReachesWaitingTask<MessagePackSerializationException>(TrainUnitHandlerName, UndeserializablePayload);
        }

        [Test]
        public void railGraphのApplySnapshot失敗は待機タスクへ例外として届く()
        {
            // デシリアライズは成功させ、applier未設定の適用段だけを失敗させる
            // Let deserialization succeed so only the apply step, with no applier wired, fails
            var payload = MessagePackSerializer.Serialize(new TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventMessagePack(null));
            AssertFailureReachesWaitingTask<NullReferenceException>(RailGraphHandlerName, payload);
        }

        [Test]
        public void trainUnitのApplySnapshot失敗は待機タスクへ例外として届く()
        {
            // Snapshotsがnullならbundleは空のまま適用段へ進み、そこで失敗する
            // A null Snapshots list keeps bundles empty and carries execution to the apply step, where it fails
            var payload = MessagePackSerializer.Serialize(new TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventMessagePack(null, 0, 0, 0));
            AssertFailureReachesWaitingTask<NullReferenceException>(TrainUnitHandlerName, payload);
        }

        // 購読コールバックを外部境界として直接叩き、失敗が呼び出し元へ抜けずに待機タスクだけへ出ることを見る
        // Invoke the subscription callback directly as the external boundary and check the failure reaches only the waiting task, never the caller
        private void AssertFailureReachesWaitingTask<TException>(string handlerName, byte[] payload) where TException : Exception
        {
            // 失敗payloadはapplierへ到達しないか到達即NREなので、applierは組み立てない
            // The failing payloads either never reach an applier or NRE on contact, so no applier is built
            var handler = new TrainFullSnapshotEventNetworkHandler(null, null, null);
            var waiting = handler.WaitForInitialApplyAsync().Preserve();

            var method = typeof(TrainFullSnapshotEventNetworkHandler).GetMethod(
                handlerName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{handlerName} がprivateメソッドとして存在しない");

            // 再送出しない代わりに適用失敗をLogErrorで残す。期待しないとNUnitがLogErrorで落とす
            // The failure is logged instead of rethrown, and an unexpected LogError would fail the test
            LogAssert.Expect(LogType.Error, new Regex("^\\[TrainFullSnapshot\\]"));

            // 初期snapshotはInitializeDispatchの同期replayを通るため、再送出すると起動ごと中断する（ADR#19）
            // The initial snapshot arrives through InitializeDispatch's synchronous replay, so a rethrow would abort startup (ADR#19)
            Assert.DoesNotThrow(() => method.Invoke(handler, new object[] { payload }), "適用失敗が呼び出し元へ再送出されている");

            Assert.AreEqual(UniTaskStatus.Faulted, waiting.Status, "適用失敗がPendingのまま残っている");
            Assert.Catch<TException>(() => waiting.GetAwaiter().GetResult());
        }
    }
}
