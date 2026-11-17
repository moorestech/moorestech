using System.IO;
using System.Linq;
using Core.Master;
using Core.Master.Validator;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Master.CleanRoom
{
    public class CleanRoomValueRangeValidatorTest
    {
        [TestCase(0.0)]
        [TestCase(-1.0)]
        public void NonPositiveFilterLifeIsRejectedTest(double filterLifeSeconds)
        {
            var blocksJToken = JToken.Parse(File.ReadAllText(MasterJsonPath("blocks.json")));
            var filterParam = blocksJToken["data"].Children<JObject>().Single(block => (string)block["name"] == "TestCleanRoomAirFilter")["blockParam"];
            filterParam["filterLifeSeconds"] = filterLifeSeconds;

            // 寿命が0以下だと消費周期が決まらないので起動時の検証で止める
            // A non-positive life leaves the consumption period undefined, so boot validation stops it
            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);
            Assert.IsFalse(isValid);
            StringAssert.Contains("[BlockMaster] Name:TestCleanRoomAirFilter has filterLifeSeconds:", errorLogs);
            StringAssert.Contains("that is not positive and finite", errorLogs);
        }

        [Test]
        public void ZeroRequiredAirChangeRateIsRejectedTest()
        {
            var json = JObject.Parse(File.ReadAllText(MasterJsonPath("cleanRoom.json")));

            // 最後の行を0にして降順の検証とは切り分け、正の値の検証だけで止まることを見る
            // Zero the last row so the descending check passes and only the positive-value check stops it
            json["data"][0]["thresholds"][3]["requiredAirChangeRate"] = 0;
            Assert.IsFalse(new CleanRoomMaster(json).Validate(out var logs));
            StringAssert.Contains("Thresholds[3] ClassName:D RequiredAirChangeRate:0 is not positive and finite", logs);
        }

        #region TestHelper

        private static string MasterJsonPath(string fileName)
        {
            // 依存マスタを初期化してから、テスト内のJSONだけを書き換える
            // Initialize the dependency masters, then edit only the in-test JSON
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", fileName);
        }

        #endregion
    }
}
