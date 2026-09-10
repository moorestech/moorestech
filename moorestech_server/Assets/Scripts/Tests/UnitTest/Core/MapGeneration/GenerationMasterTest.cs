using System;
using System.IO;
using Core.Master;
using Mooresmaster.Model.GenerationModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.UnitTest.Game.MapGeneration;

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

        [TestCase(-16)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(15)]
        [TestCase(17)]
        [TestCase(512)]
        public void 不正なdetailResolutionはバリデーションで失敗する(int detailResolution)
        {
            // 丸め値と高さ超過を拒否
            // Rejects rounded and over-height values.
            var json = LoadGenerationJson();
            json["algorithmParam"]!["detailResolution"] = detailResolution;

            var master = new GenerationMaster(json, "test");

            Assert.IsFalse(master.Validate(out var logs));
            Assert.That(logs, Does.Contain("detailResolution"));
        }

        // 未知presetを上限0として黙って潰すと、あらゆるdetailResolutionが上限超過で拒否され真因がログに出ない
        // Silently folding an unknown preset into a limit of zero rejects every detailResolution as over-limit and hides the real cause from the log
        [Test]
        public void 未知のresolutionPresetは専用のエラー行で失敗する()
        {
            var json = LoadGenerationJson();
            json["algorithmParam"]!["overrideResolution"] = 0;
            json["algorithmParam"]!["resolutionPreset"] = "_4096";

            var master = new GenerationMaster(json, "test");

            Assert.IsFalse(master.Validate(out var logs));
            Assert.That(logs, Does.Contain("is not a recognized preset"));
            Assert.That(logs, Does.Not.Contain("exceeds heightmap sample limit"));
        }

        [Test]
        public void 鉱脈帯の外半径が重複するとバリデーションで失敗する()
        {
            // 重複外半径は後続バンドがリングにならず黙って消えるため、マスタロード時に弾く
            // A duplicate outer radius silently drops the later band, so it is rejected at master load
            var json = LoadGenerationJson();
            var bands = (JArray)json["algorithmParam"]!["oreConfig"]!["entries"]![0]!["bands"]!;
            bands.Add(bands[0]!.DeepClone());

            var master = new GenerationMaster(json, "test");

            Assert.IsFalse(master.Validate(out var logs));
            Assert.IsTrue(logs.Contains("produces no ring"));
        }

        [Test]
        public void 散布entryの帯が空だとバリデーションで失敗する()
        {
            var json = LoadGenerationJsonWithGrasslandScatterBands(new JArray());

            var master = new GenerationMaster(json, "test");

            Assert.IsFalse(master.Validate(out var logs));
            Assert.IsTrue(logs.Contains("grassland.objectConfig.entries[0] has no spawn-distance bands"));
        }

        [Test]
        public void 散布帯のマイナス1以外の負の外半径はバリデーションで失敗する()
        {
            var json = LoadGenerationJsonWithGrasslandScatterBands(new JArray(
                new JObject { ["outerRadiusMeters"] = -5.0, ["pointsPerHectare"] = 1.0 }));

            var master = new GenerationMaster(json, "test");

            Assert.IsFalse(master.Validate(out var logs));
            Assert.IsTrue(logs.Contains("negative outer radius"));
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

        // grasslandへ散布entryを1件足し、bandsだけを検証対象として差し替える
        // Append one scatter entry to grassland, swapping in the bands under test
        private static JToken LoadGenerationJsonWithGrasslandScatterBands(JArray bands)
        {
            var json = LoadGenerationJson();
            var entry = new JObject
            {
                ["prefabs"] = new JArray(new JObject { ["mapObjectGuid"] = TestGenerationConfigFactory.TestMapObjectGuid }),
                ["terrainSurroundEffectType"] = "rockNoBareGround",
                ["placementMode"] = "scatter",
                ["placementParam"] = new JObject { ["bands"] = bands },
                ["scaleRange"] = new JArray(1.0, 1.0),
                ["slopeAlignment"] = 0.0,
                ["sinkRange"] = new JArray(0.0, 0.0),
                ["noiseType"] = "None",
                ["noiseFrequency"] = 10.0,
                ["noiseAmplitude"] = 1.0,
                ["noiseThreshold"] = 0.5,
                ["useSlopeFilter"] = false,
                ["slopeMin"] = 0.0,
                ["slopeMax"] = 90.0,
                ["slopeSmoothness"] = 4.0,
                ["minDistanceFromTree"] = 0.0,
                ["maxDistanceFromTree"] = 0.0,
            };
            ((JArray)json["algorithmParam"]!["grassland"]!["objectConfig"]!["entries"]!).Add(entry);
            return json;
        }
    }
}
