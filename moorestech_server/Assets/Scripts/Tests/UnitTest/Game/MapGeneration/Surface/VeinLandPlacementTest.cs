using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Core.Master;
using Game.MapGeneration.Surface;
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
            var veins = Generate(fluid, constraint, false);
            Assert.That(veins, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RejectedCandidateDoesNotStopFiniteRetriesOrRegisterGhost(bool fluid)
        {
            // 初候補を落とし後続候補が通ることを確認
            // Drop the first candidate and check later ones are accepted
            var constraint = new FirstCandidateRejectedConstraint();
            var veins = Generate(fluid, constraint, false);
            Assert.That(constraint.Calls, Is.GreaterThan(1));
            Assert.That(veins.Count, Is.GreaterThan(0));
            foreach (var vein in veins)
                Assert.That(vein.Min, Is.Not.EqualTo(constraint.Rejected.Min));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OrdinarySlopeRejectionDoesNotWarnAboutFailedEligibleCenters(bool fluid)
        {
            var veins = Generate(fluid, Land(true), true);
            Assert.That(veins, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        private static List<PlacedVein> Generate(bool fluid, IVeinLandConstraint constraint, bool rejectSlope)
        {
            var entry = VeinClusterTestFixtures.CreateEntry(VeinGuid);
            entry.useSlopeFilter = rejectSlope;
            entry.slopeMax = 0f;
            entry.slopeSmoothness = 0f;
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
            for (int x = 0; x < 65; x++) mask[z, x] = !rejectSlope || (0 < x && 0 < z && x < 64 && z < 64);
            var halo = new PlacementHaloStore(20f);
            var tile = new TilePlacementContext(0, 0, halo);
            var heights = new float[65, 65];
            for (int z = 0; z < 65; z++)
            for (int x = 0; x < 65; x++) heights[z, x] = rejectSlope ? x / 64f : 0f;
            var masks = new[] { mask };
            var biomes = new[] { BiomeType.Grassland };
            return fluid
                ? FluidVeinPlacementStage.GenerateBatch(config, masks, biomes, heights, new List<PlacementEntry>(), null, tile, constraint).Veins
                : OrePlacementStage.GenerateBatch(config, masks, biomes, heights, new List<PlacementEntry>(), null, tile, constraint).Veins;
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

            public void RecordEligibleCenter()
            {
                // 旧配置の観測は候補列や乱数を変更しない
                // Legacy observation leaves candidates and RNG untouched
            }

            public bool Overlaps(PlacedVein candidate, IReadOnlyList<PlacedVein> veins)
            {
                return VeinAabbBuilder.OverlapsAny(candidate, veins);
            }

            public bool Accept(PlacedVein candidate)
            {
                Calls++;
                if (Calls == 1) Rejected = candidate;
                return 1 < Calls && candidate.Min != Rejected.Min;
            }

            public void ReportRejections(int seed, int tileX, int tileZ, string entryGuid, int acceptedCount)
            {
                LogAssert.Expect(LogType.Warning, "Fixture rejected first candidate.");
                Debug.LogWarning("Fixture rejected first candidate.");
            }
        }
    }
}
