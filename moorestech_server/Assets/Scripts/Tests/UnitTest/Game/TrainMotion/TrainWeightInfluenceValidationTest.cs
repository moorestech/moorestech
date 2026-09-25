using System.IO;
using Core.Master;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.TrainMotion
{
    public class TrainWeightInfluenceValidationTest
    {
        // ValidateがItemMasterを引くため先に読む
        // Validate reads ItemMaster, so load it first
        [SetUp]
        public void LoadTestMaster()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void NegativeCarExponent_IsRejected()
        {
            var trainJson = ReadTestTrainJson();
            trainJson["trainCars"][0]["weightInfluenceExponent"] = -1;

            Assert.IsFalse(new TrainUnitMaster(trainJson).Validate(out var errors));
            StringAssert.Contains("WeightInfluenceExponent", errors);
        }

        [Test]
        public void NonPositiveReferenceWeight_IsRejected()
        {
            var trainJson = ReadTestTrainJson();
            trainJson["motionParameters"]["referenceWeight"] = 0;

            Assert.IsFalse(new TrainUnitMaster(trainJson).Validate(out var errors));
            StringAssert.Contains("ReferenceWeight", errors);
        }

        [Test]
        public void TestMaster_IsValid()
        {
            Assert.IsTrue(new TrainUnitMaster(ReadTestTrainJson()).Validate(out var errors), errors);
        }

        private static JObject ReadTestTrainJson()
        {
            var configs = ModJsonStringLoader.GetMasterString(new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods")));
            return JObject.Parse(configs[0].JsonContents[new JsonFileName("train")]);
        }
    }
}
