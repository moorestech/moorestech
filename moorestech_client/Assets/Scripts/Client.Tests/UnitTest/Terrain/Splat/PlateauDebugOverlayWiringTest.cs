using System;
using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Build.Placement;
using Client.Game.InGame.Environment.Terrain.Visual.Detail;
using Client.Game.InGame.Environment.Terrain.Visual.Source;
using Client.Game.InGame.Environment.Terrain.Visual.Splat;
using Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround;
using Client.Tests.UnitTest.Terrain.Surround;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Client.Tests.UnitTest.Terrain.Splat
{
    /// <summary>
    ///     SplatmapRuntimeGenerator.Generate から台地デバッグオーバーレイまでの結線を検証する。
    ///     台地の判定はパディング窓で分類したときにしか出ないので、分類チャネルのクロップまで通しで効いていないと塗られない
    ///     Exercises the wiring from SplatmapRuntimeGenerator.Generate down to the plateau debug overlay; the plateau
    ///     verdict exists only in the padded-window classification, so nothing is painted unless its crop works end to end
    /// </summary>
    public class PlateauDebugOverlayWiringTest
    {
        private const int Resolution = 129;
        private const int AlphamapResolution = 128;
        private const string DebugLayerAddress = "addr/debug0";

        [Test]
        public void PaintsAcceptedPlateausOnTheDebugColumn()
        {
            // 1画素も塗られないならオーバーレイが走っていないか、台地チャネルが空のまま渡っている
            // Not one painted pixel means either the overlay never ran or the plateau channels arrived empty
            var layerTable = CreateLayerTable(DebugLayerAddress);
            var alphamap = Generate(layerTable);

            var paintedPixels = 0;
            for (var z = 0; z < AlphamapResolution; z++)
            for (var x = 0; x < AlphamapResolution; x++)
                if (0.5f < alphamap[z, x, layerTable.DebugLayerStart]) paintedPixels++;

            Assert.Less(0, paintedPixels, "デバッグ列が塗られていない");
        }

        [Test]
        public void LeavesTheSplatmapAloneWhereNoPlateauWasAccepted()
        {
            // 全面が塗られるならマスクを読まずに塗っている。台地以外は SplatmapJob の合成が残らなければならない
            // Painting everywhere would mean ignoring the mask; outside the plateaus SplatmapJob's blend must survive
            var layerTable = CreateLayerTable(DebugLayerAddress);
            var alphamap = Generate(layerTable);

            var untouchedPixels = 0;
            for (var z = 0; z < AlphamapResolution; z++)
            for (var x = 0; x < AlphamapResolution; x++)
                if (alphamap[z, x, layerTable.DebugLayerStart] <= 0f) untouchedPixels++;

            Assert.Less(0, untouchedPixels, "台地の外まで塗られている");
        }

        private static SplatLayerTable CreateLayerTable(params string[] debugLayerAddresses)
        {
            var visualSections = CreateVisualSections();
            return SplatLayerTable.Build(
                "addr/beach", "addr/rock", visualSections.MainLayerAddresses, visualSections.TextureConfigs,
                visualSections.SurroundTextureConfigs, SurroundTestFixtures.CreateTreeSurroundSpecies(),
                debugLayerAddresses);
        }

        private static float[,,] Generate(SplatLayerTable layerTable)
        {
            var config = CreateConfig();
            var biomeTypes = new[] { BiomeType.Alpine };

            using var classification = new TerrainClassificationContext(config, biomeTypes);
            classification.Initialize();

            return SplatmapRuntimeGenerator.Generate(
                config, biomeTypes, classification, layerTable, CreateVisualSections(),
                SurroundTestFixtures.CreateTreeSurroundSpecies(),
                new float[Resolution, Resolution], CreateBiomeIndices(), AlphamapResolution,
                new List<MapObjectLayoutMessagePack>(), Vector3.zero);
        }

        private static BiomeVisualSections CreateVisualSections()
        {
            return new BiomeVisualSections(
                new[] { "addr/alpine" },
                new[] { new BiomeTextureConfig { entries = Array.Empty<TextureEntry>() } },
                new[] { new BiomeDetailConfig { entries = Array.Empty<DetailEntry>() } },
                new[] { new SurroundTextureConfig() });
        }

        // Alpineだけを有効にし、台地の検出条件を緩めて受理領域が必ず立つ地形にする
        // Enables Alpine alone and loosens the plateau thresholds so accepted regions always appear
        private static TerrainGenerationConfig CreateConfig()
        {
            var config = new TerrainGenerationConfig
            {
                overrideResolution = Resolution,
                seed = 42,
                biomeBlendRadius = 4,
                chunkPadding = 8,
                landThreshold = 0f,
                grasslandEnabled = false,
                forestEnabled = false,
                savannaEnabled = false,
                desertEnabled = false,
                mesaEnabled = false,
                alpineEnabled = true,
                jungleEnabled = false,
                woodsEnabled = false,
            };
            config.shoreConfig.minSeaRegionSize = 0;
            config.alpine.enablePlateau = true;
            config.alpine.debugPlateauOverlay = true;
            config.alpine.prominenceThreshold = 0.01f;
            config.alpine.minProminentDirections = 4;
            config.alpine.minRegionSize = 20;
            config.alpine.minPlateauCoverage = 0f;
            return config;
        }

        private static byte[,] CreateBiomeIndices()
        {
            var biomeIndices = new byte[Resolution, Resolution];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
                biomeIndices[z, x] = (byte)BiomeType.Alpine;

            return biomeIndices;
        }
    }
}
