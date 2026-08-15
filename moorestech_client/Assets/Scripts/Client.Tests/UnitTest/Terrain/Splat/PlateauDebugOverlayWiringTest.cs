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
            var alphamap = Generate(layerTable, out _);

            var paintedPixels = 0;
            for (var z = 0; z < AlphamapResolution; z++)
            for (var x = 0; x < AlphamapResolution; x++)
                if (0.5f < alphamap[z, x, layerTable.DebugLayerStart]) paintedPixels++;

            Assert.Less(0, paintedPixels, "デバッグ列が塗られていない");
        }

        [Test]
        public void LeavesTheSplatmapAloneWhereNoPlateauWasAccepted()
        {
            // デバッグ列の有無だけを変えた2走行を突き合わせる。受理領域の外が1画素でも動けば受理と棄却の区別を失っている
            // Compares two runs differing only in the debug columns: any moved pixel outside an accepted region means the accept/reject split was lost
            var withDebug = Generate(CreateLayerTable(DebugLayerAddress), out var channels);
            var withoutDebug = Generate(CreateLayerTable(), out _);

            var debugColumn = CreateLayerTable(DebugLayerAddress).DebugLayerStart;
            var comparedLayers = withoutDebug.GetLength(2);
            var changedPixels = 0;
            var strayDebugPixels = 0;
            var rejectedCandidatePixels = 0;
            for (var z = 0; z < AlphamapResolution; z++)
            for (var x = 0; x < AlphamapResolution; x++)
            {
                var source = SourcePixelIndex(z, x);
                if (0 < channels.RegionLabels[source]) continue;
                if (0f < channels.PlateauMask[source]) rejectedCandidatePixels++;
                if (0f < withDebug[z, x, debugColumn]) strayDebugPixels++;

                for (var layer = 0; layer < comparedLayers; layer++)
                    if (withDebug[z, x, layer] != withoutDebug[z, x, layer]) changedPixels++;
            }

            // 棄却候補が1つも無いと「候補を全部塗る」壊し方が素通りする。緩めた閾値でも棄却は必ず出る
            // With no rejected candidate the "paint every candidate" break would slip through; the loosened thresholds always leave some
            Assert.Less(0, rejectedCandidatePixels, "棄却された台地候補が無く、塗り過ぎを検出できない");

            // デバッグ列は受理領域だけの持ち物。棄却候補や平地に薄く乗るだけでも受理と棄却の区別を失っている
            // The debug column belongs to accepted regions alone; even a faint trace on a rejected candidate or flat ground means the split was lost
            Assert.AreEqual(0, strayDebugPixels, $"受理されていない{strayDebugPixels}画素にデバッグ列が乗っている");
            Assert.AreEqual(0, changedPixels, "受理領域の外の合成が動いている");
        }

        // ToAlphamap と同じ最近傍対応。alphamap の1画素がどの分類画素を読んだのかを引く
        // The same nearest-neighbour mapping ToAlphamap uses, resolving which classification pixel an alphamap pixel read
        private static int SourcePixelIndex(int z, int x)
        {
            var sourceX = Mathf.Clamp(
                Mathf.RoundToInt((float)x / (AlphamapResolution - 1) * (Resolution - 1)), 0, Resolution - 1);
            var sourceZ = Mathf.Clamp(
                Mathf.RoundToInt((float)z / (AlphamapResolution - 1) * (Resolution - 1)), 0, Resolution - 1);
            return sourceZ * Resolution + sourceX;
        }

        private static SplatLayerTable CreateLayerTable(params string[] debugLayerAddresses)
        {
            var visualSections = CreateVisualSections();
            return SplatLayerTable.Build(
                "addr/beach", "addr/rock", visualSections.MainLayerAddresses, visualSections.TextureConfigs,
                visualSections.SurroundTextureConfigs, SurroundTestFixtures.CreateTreeSurroundSpecies(),
                debugLayerAddresses);
        }

        private static float[,,] Generate(SplatLayerTable layerTable, out PlateauChannels channels)
        {
            var config = CreateConfig();
            var biomeTypes = new[] { BiomeType.Alpine };

            using var classification = new TerrainClassificationContext(config, biomeTypes);
            classification.Initialize();

            var alphamap = SplatmapRuntimeGenerator.Generate(
                config, biomeTypes, classification, layerTable, CreateVisualSections(),
                SurroundTestFixtures.CreateTreeSurroundSpecies(),
                new float[Resolution, Resolution], CreateBiomeIndices(), AlphamapResolution,
                new List<MapObjectLayoutMessagePack>(), Vector3.zero);

            channels = new PlateauChannels
            {
                PlateauMask = classification.Buffers.plateauMask.ToArray(),
                RegionLabels = classification.Buffers.regionLabels.ToArray(),
            };
            return alphamap;
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

        // NativeArray の寿命外で判定するため、台地の2チャネルをマネージド配列へ写す
        // Copies the two plateau channels into managed arrays so the checks outlive the NativeArrays
        private sealed class PlateauChannels
        {
            public float[] PlateauMask;
            public int[] RegionLabels;
        }
    }
}
