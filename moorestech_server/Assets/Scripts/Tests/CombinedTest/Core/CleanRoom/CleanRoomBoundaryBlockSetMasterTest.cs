using System;
using System.IO;
using Core.Master;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomBoundaryBlockSetMasterTest
    {
        private static readonly Guid WallGuid = Guid.Parse("2d8b162a-1ac5-4780-8e4e-2b8d4f6470e8");
        private static readonly Guid DoorGuid = Guid.Parse("07d7e1a2-b1fd-412f-819c-af098e79e4d4");
        private static readonly Guid ItemHatchGuid = Guid.Parse("d6268837-5326-480b-960a-ffdce4b5478e");
        private static readonly Guid PipeHatchGuid = Guid.Parse("8e425510-aa34-4a1b-9f40-5f8079183ab2");

        [Test]
        public void 部屋要約の境界と汚染接続点はマスタの集合で別々に引けるTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var master = MasterHolder.CleanRoomMaster;

            // 壁・ドア・ハッチ2種はどれも部屋要約を出す
            // Walls, doors and both hatches all show the room summary
            Assert.IsTrue(master.IsRoomSummaryBoundaryBlock(WallGuid));
            Assert.IsTrue(master.IsRoomSummaryBoundaryBlock(DoorGuid));
            Assert.IsTrue(master.IsRoomSummaryBoundaryBlock(ItemHatchGuid));
            Assert.IsTrue(master.IsRoomSummaryBoundaryBlock(PipeHatchGuid));

            // 汚染接続点はハッチ2種だけで、壁とドアは数えない
            // Only the two hatches are pollution connectors; walls and doors are not counted
            Assert.IsTrue(master.IsPollutionConnectorBlock(ItemHatchGuid));
            Assert.IsTrue(master.IsPollutionConnectorBlock(PipeHatchGuid));
            Assert.IsFalse(master.IsPollutionConnectorBlock(WallGuid));
            Assert.IsFalse(master.IsPollutionConnectorBlock(DoorGuid));

            // 集合に無いGUIDはどちらにも当たらない
            // A GUID outside the sets matches neither
            var unknown = Guid.NewGuid();
            Assert.IsFalse(master.IsRoomSummaryBoundaryBlock(unknown));
            Assert.IsFalse(master.IsPollutionConnectorBlock(unknown));
        }

        [Test]
        public void 集合の実在しないGUIDと重複は起動時の検証で止まるTest([Values("roomSummaryBoundaryBlockGuids", "pollutionConnectorBlockGuids")] string setKey)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var json = LoadTestCleanRoomJson();
            var missingGuid = Guid.Parse("00000000-0000-0000-0000-00000000dead");
            json["data"][0][setKey] = new JArray(ItemHatchGuid.ToString(), ItemHatchGuid.ToString(), missingGuid.ToString());

            Assert.IsFalse(new CleanRoomMaster(json).Validate(out var logs));
            var setName = char.ToUpperInvariant(setKey[0]) + setKey.Substring(1);
            StringAssert.Contains($"{setName} has duplicated BlockGuid:{ItemHatchGuid}", logs);
            StringAssert.Contains($"{setName} has invalid BlockGuid:{missingGuid}", logs);
        }

        [Test]
        public void テスト用マスタの集合は検証を通るTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.IsTrue(new CleanRoomMaster(LoadTestCleanRoomJson()).Validate(out var logs), logs);
        }

        #region TestHelper

        private static JObject LoadTestCleanRoomJson()
        {
            return JObject.Parse(File.ReadAllText(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "cleanRoom.json")));
        }

        #endregion
    }
}
