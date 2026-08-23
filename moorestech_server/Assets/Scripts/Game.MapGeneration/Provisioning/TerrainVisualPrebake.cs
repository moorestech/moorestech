using Core.Master;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Transfer;
using Game.Paths;
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
            WorldDataDirectory worldDataDirectory, string serverDataDirectory, TerrainTransferMeta terrainMeta, PlacementLedger ledger)
        {
            // ここで組み直すselected/configは現在のマスタ由来。転送メタの指紋と食い違えば台帳と無関係な見た目を焼くので、その前に止める
            // The selected/config rebuilt here come from the current master; disagreeing with the transfer meta's fingerprint would bake visuals unrelated to the ledger, so stop before that
            terrainMeta.ThrowIfGenerationMasterDiffers(serverDataDirectory);

            // クライアントのOpenと同じ組み立て（原点注入つきconfig）を通す。探索書き戻し後のconfigと同値になり、両者の焼き上がりが一致する
            // Go through the very assembly the client's Open uses (a config with the settled origins injected); it equals the post-write-back config, so both sides bake alike
            var selectedGeneration = MasterHolder.GenerationMaster.SelectedGeneration;
            var config = MapGenerationPipeline.BuildConfigWithSettledOrigins(
                selectedGeneration, terrainMeta.WorldSeed, serverDataDirectory, terrainMeta.Origins);

            // 高さ源にワールド本体のterrain/を選ぶ入口。共有キャッシュを高さ源にするクライアントとは入口ごと分かれている
            // The entry choosing the world's own terrain/ as the height source; a client, whose source is the shared cache, goes through a different entry entirely
            var factoryResult = TileVisualBakerFactory.CreateForPrebake(config, terrainMeta, ledger, selectedGeneration, worldDataDirectory);

            var tileCoordinates = TerrainTransferMeta.EnumerateTileCoordinates(terrainMeta.TerrainTileCount);
            foreach (var (tileX, tileZ) in tileCoordinates)
                factoryResult.Baker.Bake(tileX, tileZ);

            Debug.Log($"[TerrainVisualPrebake] Baked {tileCoordinates.Count} tiles for world '{terrainMeta.WorldId}'.");
        }
    }
}
