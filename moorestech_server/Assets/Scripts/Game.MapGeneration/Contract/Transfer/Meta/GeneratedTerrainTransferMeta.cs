using System;

namespace Game.MapGeneration.Transfer
{
    // 生成地形のワールド。地形の寸法も生成専用payloadも必ず揃っているので、消費側は有無を確かめずに読める
    // A world on generated terrain; the terrain dimensions and the generated-only payload are always present, so consumers read them without testing for absence
    public sealed class GeneratedTerrainTransferMeta : TerrainTransferMeta
    {
        public readonly int TerrainResolution;
        public readonly int TerrainTileCount;
        public readonly int TerrainChunkTotal;

        public readonly GeneratedTerrainTransferPayload GeneratedPayload;

        public GeneratedTerrainTransferMeta(
            string worldId, int terrainResolution, int terrainTileCount, int terrainChunkTotal, int worldSeed,
            GeneratedTerrainTransferPayload generatedPayload)
            : base(WorldMapMode.Generated, worldId, worldSeed)
        {
            if (generatedPayload == null) throw new ArgumentNullException(nameof(generatedPayload));

            TerrainResolution = terrainResolution;
            TerrainTileCount = terrainTileCount;
            TerrainChunkTotal = terrainChunkTotal;
            GeneratedPayload = generatedPayload;
        }

        // チャンク0本は生成失敗かファイル切り詰め。地形なしと同一視すると壊れたワールドを正常として配る
        // Zero chunks means a failed generation or truncated files; equating it with terrain-less would ship a broken world as healthy
        public void ThrowIfOwnsNoChunk()
        {
            if (0 < TerrainChunkTotal) return;
            throw new InvalidOperationException(
                $"Generated world '{WorldId}' owns zero terrain chunk: the terrain files are missing or truncated.");
        }

        // 現在のビルドとの照合(生成マスタ指紋・生成器の版)はTerrainTransferMetaCompatibilityが持つ。この型自身の値だけで完結する検査はここに置く
        // Matching against the current build (master fingerprint, generator version) lives in TerrainTransferMetaCompatibility; checks closed over this type's own values stay here
        public void ThrowIfTerrainResolutionDiffers(int currentResolution)
        {
            if (currentResolution == TerrainResolution) return;
            throw new InvalidOperationException(
                $"Generation master resolution {currentResolution} disagrees with the transferred terrain resolution {TerrainResolution}.");
        }
    }
}
