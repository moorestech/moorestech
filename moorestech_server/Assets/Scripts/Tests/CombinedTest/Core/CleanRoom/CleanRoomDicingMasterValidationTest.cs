using System.IO;
using Core.Master;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomDicingMasterValidationTest
    {
        [Test]
        public void DicingQualityApproachScaleMustBePositiveFiniteTest(
            [Values(float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0f, -1f, 0.1335f)] float value)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var json = JObject.Parse(File.ReadAllText(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "cleanRoom.json")));

            // 近づき度の尺度は正の有限値だけを通す
            // The approach scale accepts only positive finite values
            json["data"][0]["dicings"][0]["qualityApproachScale"] = value;
            var valid = new CleanRoomMaster(json).Validate(out var logs);
            Assert.AreEqual(value == 0.1335f, valid, logs);
            if (!valid) StringAssert.Contains("QualityApproachScale", logs);
        }
    }
}
