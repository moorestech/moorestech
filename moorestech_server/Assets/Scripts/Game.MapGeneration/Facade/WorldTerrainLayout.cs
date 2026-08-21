using System.Collections.Generic;
using UnityEngine;

namespace Game.MapGeneration.Facade
{
    /// <summary>
    ///     クライアントが地形を建てるのに要る値だけを運ぶ。TerrainAssetはオーサリング済み1枚、TileMapsは
    ///     生成タイルの並びと寸法で、生成システム内部の型(BiomeType・Config・クラスタ等)は一切現れない
    ///     Carries only what the client needs to stand terrain up. TerrainAsset is a single authored piece; TileMaps
    ///     is the generated tile layout and dimensions; internal generation types (BiomeType, Config, clusters, ...) never appear
    /// </summary>
    public sealed class WorldTerrainLayout
    {
        public TerrainLayoutKind Kind { get; }

        public string AuthoredTerrainDataAddress { get; }
        public Vector3 AuthoredOrigin { get; }

        public IReadOnlyList<(int TileX, int TileZ)> TileCoordinates { get; }
        public Vector3 TileSize { get; }
        public int HeightmapResolution { get; }
        public IReadOnlyList<string> TextureLayerAddresses { get; }
        public IReadOnlyList<DetailPrototypeSpec> DetailPrototypes { get; }

        public float DetailObjectDistance { get; }
        public float DetailObjectDensity { get; }

        private WorldTerrainLayout(
            TerrainLayoutKind kind, string authoredTerrainDataAddress, Vector3 authoredOrigin,
            IReadOnlyList<(int TileX, int TileZ)> tileCoordinates, Vector3 tileSize, int heightmapResolution,
            IReadOnlyList<string> textureLayerAddresses, IReadOnlyList<DetailPrototypeSpec> detailPrototypes,
            float detailObjectDistance, float detailObjectDensity)
        {
            Kind = kind;
            AuthoredTerrainDataAddress = authoredTerrainDataAddress;
            AuthoredOrigin = authoredOrigin;
            TileCoordinates = tileCoordinates;
            TileSize = tileSize;
            HeightmapResolution = heightmapResolution;
            TextureLayerAddresses = textureLayerAddresses;
            DetailPrototypes = detailPrototypes;
            DetailObjectDistance = detailObjectDistance;
            DetailObjectDensity = detailObjectDensity;
        }

        public static WorldTerrainLayout CreateTerrainAsset()
        {
            return new WorldTerrainLayout(
                TerrainLayoutKind.TerrainAsset, TerrainRenderingDefaults.TemplateTerrainDataAddress, TerrainRenderingDefaults.TemplateTerrainOrigin,
                new List<(int TileX, int TileZ)>(), Vector3.zero, 0,
                new List<string>(), new List<DetailPrototypeSpec>(),
                TerrainRenderingDefaults.TemplateDetailObjectDistance, TerrainRenderingDefaults.TemplateDetailObjectDensity);
        }

        public static WorldTerrainLayout CreateTileMaps(
            IReadOnlyList<(int TileX, int TileZ)> tileCoordinates, Vector3 tileSize, int heightmapResolution,
            IReadOnlyList<string> textureLayerAddresses, IReadOnlyList<DetailPrototypeSpec> detailPrototypes)
        {
            return new WorldTerrainLayout(
                TerrainLayoutKind.TileMaps, string.Empty, Vector3.zero,
                tileCoordinates, tileSize, heightmapResolution,
                textureLayerAddresses, detailPrototypes,
                TerrainRenderingDefaults.BakedDetailObjectDistance, TerrainRenderingDefaults.BakedDetailObjectDensity);
        }
    }
}
