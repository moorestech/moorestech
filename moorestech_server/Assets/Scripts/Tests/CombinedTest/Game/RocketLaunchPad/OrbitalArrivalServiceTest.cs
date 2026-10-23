using System.Collections.Generic;
using Game.Context;
using Game.Orbit;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Tests.CombinedTest.Game.RocketLaunchPad
{
    public class OrbitalArrivalServiceTest
    {
        [Test]
        public void 衛星の到着で成果を分割して返し到着を通知する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var service = ServerContext.GetService<IOrbitalArrivalService>();
            var arrivals = new List<OrbitalArrival>();
            service.OnOrbitalArrival.Subscribe(arrivals.Add);

            // 成果150はスタック100を超えるので2スタックに分かれる
            // An output of 150 exceeds the stack of 100, so it splits into two stacks
            var outputs = service.Arrive(ForUnitTestItemId.SatellitePayloadId, 1);

            Assert.AreEqual(2, outputs.Count);
            Assert.AreEqual(150, outputs[0].Count + outputs[1].Count);
            Assert.AreEqual(100, outputs[0].Count);
            Assert.AreEqual(50, outputs[1].Count);
            Assert.AreEqual(ForUnitTestItemId.ComputeDataId, outputs[0].Id);
            Assert.AreEqual(ForUnitTestItemId.ComputeDataId, outputs[1].Id);
            Assert.AreEqual(1, arrivals.Count);
            Assert.AreEqual(ForUnitTestItemId.SatellitePayloadId, arrivals[0].PayloadItemId);
            Assert.AreEqual(1, arrivals[0].PayloadCount);
        }

        [Test]
        public void 成果の無い積荷は空を返し通知はする()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var service = ServerContext.GetService<IOrbitalArrivalService>();
            var arrivals = new List<OrbitalArrival>();
            service.OnOrbitalArrival.Subscribe(arrivals.Add);

            Assert.AreEqual(0, service.Arrive(ForUnitTestItemId.CablePayloadId, 10).Count);
            Assert.AreEqual(1, arrivals.Count);
            Assert.AreEqual(ForUnitTestItemId.CablePayloadId, arrivals[0].PayloadItemId);
            Assert.AreEqual(10, arrivals[0].PayloadCount);
        }

        [Test]
        public void 成果の先読みは到着と同じ量を返し通知しない()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var service = ServerContext.GetService<IOrbitalArrivalService>();
            var arrivals = new List<OrbitalArrival>();
            service.OnOrbitalArrival.Subscribe(arrivals.Add);

            // 先読みは通知せず、到着と同じスタックの並びを返す
            // The preview publishes nothing and returns the same stacks as the arrival
            var preview = service.PreviewArrivalOutputs(ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.AreEqual(0, arrivals.Count);
            var outputs = service.Arrive(ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.AreEqual(outputs.Count, preview.Count);
            for (var i = 0; i < outputs.Count; i++)
            {
                Assert.AreEqual(outputs[i].Id, preview[i].Id);
                Assert.AreEqual(outputs[i].Count, preview[i].Count);
                Assert.AreNotSame(outputs[i], preview[i]);
            }
            Assert.Throws<System.ArgumentException>(() => service.PreviewArrivalOutputs(ForUnitTestItemId.CablePayloadId, 3));
        }

        [Test]
        public void 表に無い積荷と半端な個数は例外()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var service = ServerContext.GetService<IOrbitalArrivalService>();
            var arrivals = new List<OrbitalArrival>();
            service.OnOrbitalArrival.Subscribe(arrivals.Add);

            // 不正な呼び出しは成果と到着の両方を発生させない
            // Invalid requests produce neither outputs nor arrival notifications
            Assert.IsTrue(service.IsOrbitalPayload(ForUnitTestItemId.SatellitePayloadId));
            Assert.IsTrue(service.IsOrbitalPayload(ForUnitTestItemId.CablePayloadId));
            Assert.IsFalse(service.IsOrbitalPayload(ForUnitTestItemId.ComputeDataId));
            Assert.Throws<System.InvalidOperationException>(() => service.Arrive(ForUnitTestItemId.ComputeDataId, 1));
            Assert.Throws<System.InvalidOperationException>(() => service.PreviewArrivalOutputs(ForUnitTestItemId.ComputeDataId, 1));
            Assert.Throws<System.ArgumentException>(() => service.Arrive(ForUnitTestItemId.CablePayloadId, 3));
            Assert.Throws<System.ArgumentException>(() => service.Arrive(ForUnitTestItemId.CablePayloadId, 0));
            Assert.Throws<System.ArgumentException>(() => service.Arrive(ForUnitTestItemId.CablePayloadId, -5));
            Assert.AreEqual(0, arrivals.Count);
        }

        [Test]
        public void 初打ち上げの通知は1回だけ()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var history = ServerContext.GetService<IRocketLaunchHistory>();
            var count = 0;
            history.OnFirstLaunch.Subscribe(_ => count++);

            Assert.IsFalse(history.HasLaunchedOnce);
            history.RecordLaunch();
            history.RecordLaunch();

            Assert.IsTrue(history.HasLaunchedOnce);
            Assert.AreEqual(1, count);
        }

        [Test]
        public void 複数便の到着は成果を便数倍して通知を1件流す()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var service = ServerContext.GetService<IOrbitalArrivalService>();
            var arrivals = new List<OrbitalArrival>();
            service.OnOrbitalArrival.Subscribe(arrivals.Add);

            // 2便の成果300は100ずつの3スタックに分かれる
            // Two launches yield 300 outputs split into three stacks of 100
            var outputs = service.Arrive(ForUnitTestItemId.SatellitePayloadId, 2);
            Assert.AreEqual(3, outputs.Count);
            foreach (var output in outputs)
            {
                Assert.AreEqual(ForUnitTestItemId.ComputeDataId, output.Id);
                Assert.AreEqual(100, output.Count);
            }
            Assert.AreEqual(1, arrivals.Count);
            Assert.AreEqual(2, arrivals[0].PayloadCount);
        }

        [Test]
        public void 履歴の復元は初回通知を流さず未発射のワールドへ戻せる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var history = ServerContext.GetService<IRocketLaunchHistory>();
            var count = 0;
            history.OnFirstLaunch.Subscribe(_ => count++);

            // 発射済みの復元と再記録は初回発射として通知しない
            // Restoring a launched world and recording again do not notify a first launch
            history.LoadSaveData(true);
            Assert.IsTrue(history.HasLaunchedOnce);
            history.RecordLaunch();
            Assert.AreEqual(0, count);

            // 未発射の復元後は最初の実発射だけを通知する
            // After restoring an unlaunched world, notify only its first actual launch
            history.LoadSaveData(false);
            Assert.IsFalse(history.HasLaunchedOnce);
            Assert.AreEqual(0, count);
            history.RecordLaunch();
            history.RecordLaunch();
            Assert.IsTrue(history.HasLaunchedOnce);
            Assert.AreEqual(1, count);
        }
    }
}
