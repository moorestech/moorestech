using Core.Master;
using Core.Update;
using Game.CleanRoom;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomPurityTest
    {
        [Test]
        public void SealedRoomAccumulatesGeometricPollutionTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            // 内寸1セル（V=1, S=6, 接続点0）の箱では A_total = 0.1×1 + 0.05×6 = 0.4/秒
            // A single-cell box (V=1, S=6, no connectors) yields A_total = 0.1x1 + 0.05x6 = 0.4/sec
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));

            // 初回tickで検出直後から積分が始まり、100tick（5秒）で N≈2.0 になる
            // Integration starts right after detection on tick 1, reaching N≈2.0 after 100 ticks (5 sec)
            for (var i = 0; i < 100; i++) GameUpdater.UpdateOneTick();
            Assert.IsTrue(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out var room));
            Assert.AreEqual(2.0, room.ImpurityCount, 0.02);

            // 清浄機なし（ACH=0）ではどの閾値行も満たせず行は Out のまま
            // With no purifier (ACH=0) no threshold row is satisfiable, so the row stays Out
            Assert.AreEqual(MasterHolder.CleanRoomMaster.OutThresholdIndex, room.ThresholdIndex);

            // さらに300tick回しても N は幾何項ぶん線形増加し続け、行は Out を維持する
            // Another 300 ticks keep N growing linearly by the geometric terms with the row still Out
            for (var i = 0; i < 300; i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(8.0, room.ImpurityCount, 0.08);
            Assert.AreEqual(MasterHolder.CleanRoomMaster.OutThresholdIndex, room.ThresholdIndex);
        }
    }
}
