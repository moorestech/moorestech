using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Core.Master;
using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Surface;
using Game.MapGeneration.Pipeline.Surface.Placement;
using Game.MapGeneration.Pipeline.Tiling;
using Mod.Config;
using Mod.Loader;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.UnitTest.Game.MapGeneration.Vein;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    public class VeinLandPlacementTest
    {
        private const string VeinGuid = "11111111-0000-0000-0000-000000000001";

        [SetUp]
        public void SetUp()
        {
            var resources = new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods"));
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(resources)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AllSeaFinishesWithNoConfirmedItemOrFluidVeins(bool fluid)
        {
            var constraint = Land(false);
            LogAssert.Expect(LogType.Warning, new Regex("seed=196.*coast=[1-9][0-9]*"));
            var veins = Generate(fluid, constraint);
            Assert.That(veins, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RejectedCandidateDoesNotStopFiniteRetriesOrRegisterGhost(bool fluid)
        {
            // 最初の候補を落として、同じ既存ループの後続候補が通ることを調べる
            // Reject the first candidate and exercise acceptance of later candidates in the same existing loop
            var constraint = new FirstCandidateRejectedConstraint();
            var veins = Generate(fluid, constraint);
            Assert.That(constraint.Calls, Is.GreaterThan(1));
            Assert.That(veins.Count, Is.GreaterThan(0));
            foreach (var vein in veins)
                Assert.That(vein.Min, Is.Not.EqualTo(constraint.Rejected.Min));
        }

        private static List<PlacedVein> Generate(bool fluid, IVeinLandConstraint constraint)
        {
            var entry = VeinClusterTestFixtures.CreateEntry(VeinGuid);
            var config = new TerrainGenerationConfig
            {
                seed = 196, terrainWidth = 250f, terrainLength = 250f, terrainHeight = 100f,
                overrideResolution = 65, gridSizeX = 1, gridSizeZ = 1, generateOre = true,
                oreConfig = new WorldOreConfig
                {
                    entries = fluid ? new OreEntry[0] : new[] { entry },
                    fluidEntries = fluid ? new[] { entry } : new OreEntry[0], borderMargin = 0f,
                },
            };
            var mask = new bool[65, 65];
            for (int z = 0; z < 65; z++)
            for (int x = 0; x < 65; x++) mask[z, x] = true;
            var halo = new PlacementHaloStore(20f);
            var tile = new TilePlacementContext(0, 0, halo);
            var masks = new[] { mask };
            var biomes = new[] { BiomeType.Grassland };
            return fluid
                ? FluidVeinPlacementStage.Generate(config, masks, biomes, new float[65, 65], new List<PlacementEntry>(), null, tile, constraint)
                : OrePlacementStage.Generate(config, masks, biomes, new float[65, 65], new List<PlacementEntry>(), null, tile, constraint);
        }

        private static GroundedVeinLandConstraint Land(bool land)
        {
            var geometry = new SurfaceLattice(new Vector2(-1000f, -1000f), Vector2.one * 100f, 31, 31);
            var mask = new bool[31 * 31];
            for (int i = 0; i < mask.Length; i++) mask[i] = land;
            return new GroundedVeinLandConstraint(new LandCellField(geometry, mask), Vector2.zero, SurfaceEnvelope.GeneratedV5);
        }

        private sealed class FirstCandidateRejectedConstraint : IVeinLandConstraint
        {
            public int Calls { get; private set; }
            public PlacedVein Rejected { get; private set; }

            public bool Accept(PlacedVein candidate)
            {
                Calls++;
                if (Calls == 1) Rejected = candidate;
                return Calls > 1 && candidate.Min != Rejected.Min;
            }

            public void ReportRejections(int seed, int tileX, int tileZ, string entryGuid)
            {
                LogAssert.Expect(LogType.Warning, "Fixture rejected first candidate.");
                Debug.LogWarning("Fixture rejected first candidate.");
            }
        }
    }
}
