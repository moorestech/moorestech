using System.IO;
using Core.Master;
using Mooresmaster.Model.GenerationModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.MapGeneration
{
    public class GenerationVeinGuidUniquenessTest
    {
        [SetUp]
        public void Setup()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void OreEntries内のVeinGuid重複は対象コレクションとGuidを報告する()
        {
            var json = LoadGenerationJson();
            var entries = (JArray)json["algorithmParam"]!["oreConfig"]!["entries"]!;
            entries.Add(entries[0]!.DeepClone());

            var master = new GenerationMaster(json, "test");

            Assert.IsFalse(master.Validate(out var logs));
            StringAssert.Contains("oreConfig.entries has duplicate VeinGuid:", logs);
            StringAssert.Contains(entries[0]!["veinGuid"]!.Value<string>(), logs);
        }

        [Test]
        public void FluidEntries内のVeinGuid重複は対象コレクションとGuidを報告する()
        {
            var json = LoadGenerationJson();
            var entries = (JArray)json["algorithmParam"]!["oreConfig"]!["fluidEntries"]!;
            entries.Add(entries[0]!.DeepClone());

            var master = new GenerationMaster(json, "test");

            Assert.IsFalse(master.Validate(out var logs));
            StringAssert.Contains("oreConfig.fluidEntries has duplicate VeinGuid:", logs);
            StringAssert.Contains(entries[0]!["veinGuid"]!.Value<string>(), logs);
        }

        [Test]
        public void OreEntriesとFluidEntries間でVeinGuidが一致しても重複とは報告しない()
        {
            var json = LoadGenerationJson();
            var oreEntries = (JArray)json["algorithmParam"]!["oreConfig"]!["entries"]!;
            var fluidEntries = (JArray)json["algorithmParam"]!["oreConfig"]!["fluidEntries"]!;
            fluidEntries[0]!["veinGuid"] = oreEntries[0]!["veinGuid"]!.Value<string>();

            var master = new GenerationMaster(json, "test");

            Assert.IsFalse(master.Validate(out var logs));
            StringAssert.Contains("references a non-fluid vein", logs);
            StringAssert.DoesNotContain("oreConfig.entries has duplicate VeinGuid:", logs);
            StringAssert.DoesNotContain("oreConfig.fluidEntries has duplicate VeinGuid:", logs);
        }

        private static JToken LoadGenerationJson()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "generation.json");
            return JToken.Parse(File.ReadAllText(path));
        }
    }
}
