using Game.Block.Interface;
using Game.CleanRoom.Info;
using Game.CleanRoom.Info.Leak;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom.Usability
{
    public class CleanRoomLeakProbeTest
    {
        [Test]
        public void SingleMissingWallReportsThatCellTest()
        {
            CleanRoomHatchTest.CreateServer();
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(2, 2, 0), BlockRemoveReason.ManualRemove);

            // 室内から探すと、南の壁に空いた1マスだけが漏れとして数えられる
            // Probing from inside counts only the single hole in the south wall
            var report = AsOpenings(CleanRoomLeakProbe.ProbeFromSeeds(ServerContext.WorldBlockDatastore, new[] { new Vector3Int(2, 2, 2) }));
            Assert.AreEqual(1, report.TotalOpeningCount);
        }

        [Test]
        public void MissingTopFaceReportsTheWholeHoleTest()
        {
            CleanRoomHatchTest.CreateServer();
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(6, 4, 6));
            for (var x = 1; x <= 5; x++)
            for (var z = 1; z <= 5; z++)
                ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(x, 4, z), BlockRemoveReason.ManualRemove);

            // 境界が上の段まで続くので箱を y=4 まで広げ、抜けた上面の25マスを漏れとして返す
            // The boundary continues upward, so the box grows to y=4 and the 25 missing top-face cells are returned
            var report = AsOpenings(CleanRoomLeakProbe.ProbeFromSeeds(ServerContext.WorldBlockDatastore, new[] { new Vector3Int(3, 2, 3) }));
            Assert.AreEqual(25, report.TotalOpeningCount);
        }

        [Test]
        public void OversizedSealedBoxIsTooLargeTest()
        {
            CleanRoomHatchTest.CreateServer();

            // 内寸17x15x17=4335 は部屋の上限4096を超える
            // An interior of 17x15x17=4335 exceeds the 4096-cell room cap
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(18, 16, 18));
            var report = CleanRoomLeakProbe.ProbeFromSeeds(ServerContext.WorldBlockDatastore, new[] { new Vector3Int(9, 8, 9) });
            Assert.IsInstanceOf<CleanRoomTooLargeLeak>(report);
        }

        [Test]
        public void IsolatedWallHasNoEnclosureTest()
        {
            CleanRoomHatchTest.CreateServer();
            CleanRoomDetectionTest.AddBlock(ForUnitTestModBlockId.CleanRoomWallId, new Vector3Int(0, 0, 0));

            // 壁の隣から探しても囲いが無いので、囲い無しの型になる
            // Probing next to the lone wall finds no enclosure, so the result is the no-enclosure type
            var report = CleanRoomLeakProbe.ProbeFromSeeds(ServerContext.WorldBlockDatastore, new[] { new Vector3Int(1, 0, 0) });
            Assert.IsInstanceOf<CleanRoomNoEnclosureLeak>(report);
        }

        [Test]
        public void SealedButUndetectedBoxIsDetectionPendingTest()
        {
            CleanRoomHatchTest.CreateServer();

            // tick を進めず検出前のまま探すと、穴が無いので検出待ちになる
            // Probing before any tick finds no hole, so the result is pending detection
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            var report = CleanRoomLeakProbe.ProbeFromSeeds(ServerContext.WorldBlockDatastore, new[] { new Vector3Int(2, 2, 2) });
            Assert.IsInstanceOf<CleanRoomDetectionPendingLeak>(report);
        }

        #region TestHelper

        private static CleanRoomOpeningsLeak AsOpenings(CleanRoomLeakReport report)
        {
            Assert.IsInstanceOf<CleanRoomOpeningsLeak>(report, $"expected openings but got {report.GetType().Name}");
            return (CleanRoomOpeningsLeak)report;
        }

        #endregion
    }
}
