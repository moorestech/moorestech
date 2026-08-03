using System.Reflection;
using Client.Game.InGame.Train.Network;
using Cysharp.Threading.Tasks;
using MessagePack;
using NUnit.Framework;

namespace Client.Tests
{
    public class TrainFullSnapshotFailurePropagationTest
    {
        [Test]
        public void trainUnitSnapshotの適用失敗は待機タスクへ例外として届く()
        {
            var handler = CreateHandlerWithoutAppliers();
            var waiting = handler.WaitForInitialApplyAsync().Preserve();

            InvokeSnapshotHandler(handler, "HandleTrainUnitFullSnapshot");

            Assert.AreEqual(UniTaskStatus.Faulted, waiting.Status, "適用失敗がPendingのまま残っている");
            Assert.Catch<MessagePackSerializationException>(() => waiting.GetAwaiter().GetResult());
        }

        [Test]
        public void railGraphSnapshotの適用失敗は待機タスクへ例外として届く()
        {
            var handler = CreateHandlerWithoutAppliers();
            var waiting = handler.WaitForInitialApplyAsync().Preserve();

            InvokeSnapshotHandler(handler, "HandleRailGraphFullSnapshot");

            Assert.AreEqual(UniTaskStatus.Faulted, waiting.Status, "適用失敗がPendingのまま残っている");
            Assert.Catch<MessagePackSerializationException>(() => waiting.GetAwaiter().GetResult());
        }

        // デシリアライズ不能なpayloadはapplierへ到達する前に失敗するのでapplierは組み立てない
        // An undeserializable payload fails before reaching any applier, so none of them are built here
        private TrainFullSnapshotEventNetworkHandler CreateHandlerWithoutAppliers()
        {
            return new TrainFullSnapshotEventNetworkHandler(null, null, null);
        }

        // 購読コールバックを外部境界として直接叩き、例外を握り潰していないことを見る
        // Invoke the subscription callback directly as the external boundary to see failures are not swallowed
        private void InvokeSnapshotHandler(TrainFullSnapshotEventNetworkHandler handler, string methodName)
        {
            var method = typeof(TrainFullSnapshotEventNetworkHandler).GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} がprivateメソッドとして存在しない");

            // MessagePackが決して使わない0xC1を流し、デシリアライズ段で確実に失敗させる
            // Feed 0xC1, a byte MessagePack never emits, so deserialization fails for certain
            method.Invoke(handler, new object[] { new byte[] { 0xC1 } });
        }
    }
}
