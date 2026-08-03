using System;
using System.Reflection;
using Client.Game.InGame.Train.Network;
using Cysharp.Threading.Tasks;
using MessagePack;
using NUnit.Framework;
using Server.Event.EventReceive;

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

        // 購読コールバックを外部境界として直接叩き、失敗が待機タスクとディスパッチ側の両方へ出ることを見る
        // Invoke the subscription callback directly as the external boundary and check the failure surfaces to both the waiting task and the dispatcher
        private void AssertFailureReachesWaitingTask<TException>(string handlerName, byte[] payload) where TException : Exception
        {
            // 失敗payloadはapplierへ到達しないか到達即NREなので、applierは組み立てない
            // The failing payloads either never reach an applier or NRE on contact, so no applier is built
            var handler = new TrainFullSnapshotEventNetworkHandler(null, null, null);
            var waiting = handler.WaitForInitialApplyAsync().Preserve();

            var method = typeof(TrainFullSnapshotEventNetworkHandler).GetMethod(
                handlerName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{handlerName} がprivateメソッドとして存在しない");

            // 再送出された例外はリフレクション越しにTargetInvocationExceptionへ包まれる
            // A rethrown exception arrives wrapped in TargetInvocationException when invoked through reflection
            var invocationFailure = Assert.Throws<TargetInvocationException>(() => method.Invoke(handler, new object[] { payload }));
            Assert.That(invocationFailure.InnerException, Is.InstanceOf<TException>(), "適用失敗が呼び出し元へ再送出されていない");

            Assert.AreEqual(UniTaskStatus.Faulted, waiting.Status, "適用失敗がPendingのまま残っている");
            Assert.Catch<TException>(() => waiting.GetAwaiter().GetResult());
        }
    }
}
