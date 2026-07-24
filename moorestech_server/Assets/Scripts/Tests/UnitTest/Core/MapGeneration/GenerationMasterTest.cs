using System;
using System.IO;
using Core.Master;
using Mooresmaster.Model.GenerationModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.MapGeneration
{
    /// <summary>
    ///     GenerationMasterのロード・選択・veinType一致バリデーションを検証するテスト
    ///     Tests for GenerationMaster's load, selection, and veinType-match validation
    /// </summary>
    public class GenerationMasterTest
    {
        // ForUnitTest map.json に定義済みのテスト用鉱脈GUID
        // Test vein GUIDs defined in ForUnitTest map.json
        private static readonly Guid ItemVeinGuid = Guid.Parse("11111111-0000-0000-0000-000000000001");
        private static readonly Guid FluidVeinGuid = Guid.Parse("11111111-0000-0000-0000-000000000002");

        [SetUp]
        public void Setup()
        {
            // DIコンテナ生成でMasterHolderをForUnitTest modからロードする
            // Load MasterHolder from ForUnitTest mod via DI container generation
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void ForUnitTestのgeneration_jsonからVanillaGeneratorが選択される()
        {
            var selected = MasterHolder.GenerationMaster.SelectedGeneration;

            Assert.IsTrue(MasterHolder.GenerationMaster.HasSelectedGeneration);
            Assert.NotNull(selected);
            Assert.AreEqual(Generation.AlgorithmConst.VanillaGenerator, selected.Algorithm);
            Assert.AreEqual(1000, selected.Priority);
        }

        [Test]
        public void OreEntryのVeinGuidがfluid鉱脈を指すとバリデーションで失敗する()
        {
            // 正規のgeneration.jsonを読み込み、oreConfig.entries[0].veinGuidをfluid鉱脈へ差し替える
            // Load the real generation.json and swap oreConfig.entries[0].veinGuid to a fluid vein
            var json = LoadGenerationJsonWithFirstOreEntryVeinGuid(FluidVeinGuid);

            var master = new GenerationMaster(json, "test");

            Assert.IsFalse(master.Validate(out var logs));
            Assert.IsTrue(logs.Contains("references a non-item vein"));
        }

        [Test]
        public void FluidVeinEntryのVeinGuidがitem鉱脈を指すとバリデーションで失敗する()
        {
            // fluidEntries[0].veinGuidをitem鉱脈へ差し替えて型不一致を検証
            // Swap fluidEntries[0].veinGuid to an item vein to check the type mismatch
            var json = LoadGenerationJsonWithFirstFluidEntryVeinGuid(ItemVeinGuid);

            var master = new GenerationMaster(json, "test");

            Assert.IsFalse(master.Validate(out var logs));
            Assert.IsTrue(logs.Contains("references a non-fluid vein"));
        }

        [Test]
        public void 正しいveinType参照のgeneration_jsonはバリデーションを通過する()
        {
            var json = LoadGenerationJson();

            var master = new GenerationMaster(json, "test");

            Assert.IsTrue(master.Validate(out var logs));
            Assert.IsEmpty(logs);
        }

        private static JToken LoadGenerationJson()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "generation.json");
            return JToken.Parse(File.ReadAllText(path));
        }

        private static JToken LoadGenerationJsonWithFirstOreEntryVeinGuid(Guid veinGuid)
        {
            var json = LoadGenerationJson();
            json["algorithmParam"]!["oreConfig"]!["entries"]![0]!["veinGuid"] = veinGuid.ToString();
            return json;
        }

        private static JToken LoadGenerationJsonWithFirstFluidEntryVeinGuid(Guid veinGuid)
        {
            var json = LoadGenerationJson();
            json["algorithmParam"]!["oreConfig"]!["fluidEntries"]![0]!["veinGuid"] = veinGuid.ToString();
            return json;
        }
    }
}
