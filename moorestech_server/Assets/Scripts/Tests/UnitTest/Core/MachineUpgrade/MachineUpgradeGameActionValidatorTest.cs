using System.IO;
using Core.Master;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.MachineUpgrade
{
    /// <summary>
    ///     強化系アクション（製作上限・スロット上限）の引数検証を確かめる
    ///     Checks the param validation of machine-upgrade actions (craft cap, slot limit)
    /// </summary>
    public class MachineUpgradeGameActionValidatorTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void TestMasterResearchPassesTest()
        {
            Assert.IsTrue(new ResearchMaster(LoadResearchJson()).Validate(out var logs), logs);
        }

        [Test]
        public void CraftCapOnUncappedTargetFailsTest()
        {
            // 上限なし（Inherit）の継承先に上限を付けても効かないので拒否する
            // A cap on an uncapped (Inherit) target would have no effect, so it is refused
            var json = LoadResearchJson();
            CraftCapParam(json)["targetItemGuids"][0] = ForUnitTestLevelInheritanceIds.TargetBaseGuid.ToString();

            Assert.IsFalse(new ResearchMaster(json).Validate(out var logs));
            StringAssert.Contains("is not the target of any InheritUpToUnlocked recipe", logs);
        }

        [Test]
        public void CraftCapLevelBeyondFamilyFailsTest()
        {
            // ファミリー長3を超えるレベルは拒否する
            // A level beyond the family length of 3 is refused
            var json = LoadResearchJson();
            CraftCapParam(json)["level"] = 4;

            Assert.IsFalse(new ResearchMaster(json).Validate(out var logs));
            StringAssert.Contains("is outside 1..3", logs);
        }

        [Test]
        public void ModuleSlotStackLevelBeyondTableFailsTest()
        {
            // 段階表 [3, 6] の最大レベルは1
            // The max level of the [3, 6] table is 1
            var json = LoadResearchJson();
            FindNode(json, ForUnitTestLevelInheritanceIds.ModuleSlotStackResearchGuid.ToString())["clearedActions"][0]["gameActionParam"]["level"] = 2;

            Assert.IsFalse(new ResearchMaster(json).Validate(out var logs));
            StringAssert.Contains("unlockModuleSlotStackLevel level 2 is outside 0..1", logs);
        }

        private static JToken CraftCapParam(JObject json)
        {
            return FindNode(json, ForUnitTestLevelInheritanceIds.CraftCapResearchGuid.ToString())["clearedActions"][0]["gameActionParam"];
        }

        private static JObject LoadResearchJson()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "research.json");
            return JObject.Parse(File.ReadAllText(path));
        }

        private static JObject FindNode(JObject json, string researchGuid)
        {
            foreach (var node in (JArray)json["data"])
            {
                if (node["researchNodeGuid"].Value<string>() == researchGuid) return (JObject)node;
            }
            Assert.Fail($"research node not found: {researchGuid}");
            return null;
        }
    }
}
