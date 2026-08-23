using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Mooresmaster.Model.GenerationModule;
using UnityEngine;

namespace Game.MapGeneration.Provisioning
{
    /// <summary>
    ///     ワールド生成直後に共有キャッシュへ全タイルを焼く。同じPCのクライアントは初回起動で pass-2（splat/detailの再計算）を省ける
    ///     pass-1（配置台帳）と表示用高さの木摂動は Open/Bake が毎回計算する。ここまでキャッシュに含める案は実測10秒ゲート後の後続候補
    ///     Bakes every tile into the shared cache right after world generation; a same-PC client skips pass-2 (splat/detail) at first start
    ///     pass-1 (the ledger) and the tree perturbation of display heights are still computed by Open/Bake; caching those too is a follow-up behind the 10s gate
    /// </summary>
    public static class TerrainVisualPrebake
    {
        public static void BakeAll(
            WorldDataDirectory worldDataDirectory, TerrainTransferMeta terrainMeta, TerrainGenerationConfig config,
            PlacementLedger ledger, Generation selectedGeneration, string generationMasterFingerprint)
        {
            // 台帳・configは同じ生成呼び出し由来。fingerprintが転送メタと食い違うのはその前提が崩れた合図で、無言のまま焼き続けない
            // The ledger and config come from the very same generation call; a fingerprint disagreeing with the transfer meta signals that premise broke, so this never bakes on in silence
            terrainMeta.ThrowIfGenerationMasterFingerprintDiffers(generationMasterFingerprint);

            // 先焼きの高さ源はワールド本体のterrain/(生成した本人が唯一の正)。共有キャッシュへの複製は要らない
            // The prebake's height source is the world's own terrain/ (the generator itself is the sole truth); no copy into the shared cache is needed
            var factoryResult = TileVisualBakerFactory.Create(config, terrainMeta, ledger, worldDataDirectory, selectedGeneration);

            var tileCoordinates = TerrainTransferMeta.EnumerateTileCoordinates(terrainMeta.TerrainTileCount);
            foreach (var (tileX, tileZ) in tileCoordinates)
                factoryResult.Baker.Bake(tileX, tileZ);

            Debug.Log($"[TerrainVisualPrebake] Baked {tileCoordinates.Count} tiles for world '{terrainMeta.WorldId}'.");
        }
    }
}
