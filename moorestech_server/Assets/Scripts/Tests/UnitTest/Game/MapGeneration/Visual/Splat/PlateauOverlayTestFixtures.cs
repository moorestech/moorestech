using System;
using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Visual.Surround;
using Tests.UnitTest.Game.MapGeneration.Visual.Surround;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Splat
{
    /// <summary>
    ///     台地デバッグオーバーレイを通しで回すための共通素材。結線テストとゲートのテストが同じ地形・同じ列構成を見ないと、
    ///     片方だけが受理領域の出る設定になって「塗られなかった」理由を取り違える
    ///     The shared material for running the plateau debug overlay end to end; unless the wiring test and the gate test see
    ///     the same terrain and columns, only one of them gets accepted regions and "nothing was painted" gets misread
    /// </summary>
    public static class PlateauOverlayTestFixtures
    {
        public const int Resolution = 129;
        public const int AlphamapResolution = 128;
        public const string DebugLayerAddress = "addr/debug0";

        public static SplatLayerTable CreateLayerTable(params string[] debugLayerAddresses)
        {
            var visualSections = CreateVisualSections();
            return SplatLayerTable.Build(
                "addr/beach", "addr/rock", visualSections.MainLayerAddresses, visualSections.TextureConfigs,
                visualSections.SurroundTextureConfigs, SurroundTestFixtures.CreateTreeSurroundSpecies(),
                debugLayerAddresses);
        }

        public static float[,,] Generate(TerrainGenerationConfig config, SplatLayerTable layerTable, out PlateauChannels channels)
        {
            var biomeTypes = new[] { BiomeType.Alpine };

            using var classification = new TileClassificationContext(config, biomeTypes);
            classification.Initialize();

            var alphamap = SplatmapStage.Generate(
                config, biomeTypes, classification, layerTable, CreateVisualSections(),
                SurroundTestFixtures.CreateTreeSurroundSpecies(),
                new float[Resolution, Resolution], CreateBiomeIndices(), AlphamapResolution,
                new List<LedgerPlacement>(), Vector3.zero);

            channels = new PlateauChannels
            {
                PlateauMask = classification.Buffers.plateauMask.ToArray(),
                RegionLabels = classification.Buffers.regionLabels.ToArray(),
            };
            return alphamap;
        }

        // ToAlphamap と同じ最近傍対応。alphamap の1画素がどの分類画素を読んだのかを引く
        // The same nearest-neighbour mapping ToAlphamap uses, resolving which classification pixel an alphamap pixel read
        public static int SourcePixelIndex(int z, int x)
        {
            var sourceX = Mathf.Clamp(
                Mathf.RoundToInt((float)x / (AlphamapResolution - 1) * (Resolution - 1)), 0, Resolution - 1);
            var sourceZ = Mathf.Clamp(
                Mathf.RoundToInt((float)z / (AlphamapResolution - 1) * (Resolution - 1)), 0, Resolution - 1);
            return sourceZ * Resolution + sourceX;
        }

        // Alpineだけを有効にし、台地の検出条件を緩めて受理領域が必ず立つ地形にする
        // Enables Alpine alone and loosens the plateau thresholds so accepted regions always appear
        public static TerrainGenerationConfig CreateConfig()
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

        // Alpineのベース色だけだと台地画素が最初から「ベース1.0」になり、オーバーレイの全消し塗り潰しが差として現れない
        // With Alpine's base colour alone the plateau pixels already read "base 1.0" and the overlay's wipe leaves no difference to see
        private static BiomeVisualSections CreateVisualSections()
        {
            var secondaryLayer = new TextureEntry { layerAddressablePath = "addr/alpine-secondary", weight = 0.5f };
            return new BiomeVisualSections(
                new[] { "addr/alpine" },
                new[] { new BiomeTextureConfig { entries = new[] { secondaryLayer } } },
                new[] { new BiomeDetailConfig { entries = Array.Empty<DetailEntry>() } },
                new[]
                {
                    new SurroundTextureConfig
                    {
                        surroundLayerAddressablePath = SurroundTestFixtures.MudLayerAddress,
                    },
                });
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
        public sealed class PlateauChannels
        {
            public float[] PlateauMask;
            public int[] RegionLabels;
        }
    }
}
