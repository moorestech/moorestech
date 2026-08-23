using System;
using Core.Master;
using Game.MapGeneration.Identity;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Transfer;
using Game.Paths;
using UnityEngine;

namespace Game.MapGeneration.Facade
{
    /// <summary>
    ///     生成システムが外へ見せる唯一の入口。ワールド同一性（転送メタ）を受け取り、結果だけを返す。
    ///     実際に生成したのか固定の地形を返しただけなのか、キャッシュを引いたのかは外から区別できない
    ///     The single entry the generation system exposes: takes the world identity (transfer meta) and returns results only.
    ///     Whether it generated, returned an authored terrain, or hit a cache is indistinguishable from outside
    /// </summary>
    public sealed class WorldTerrainSession
    {
        private readonly TileVisualBaker _baker;
        public WorldTerrainLayout Layout { get; }

        private WorldTerrainSession(WorldTerrainLayout layout, TileVisualBaker baker)
        {
            Layout = layout;
            _baker = baker;
        }

        public static WorldTerrainSession Open(TerrainTransferMeta terrainMeta, string serverDataDirectory)
        {
            if (terrainMeta.IsTemplate) return new WorldTerrainSession(WorldTerrainLayout.CreateTerrainAsset(), null);

            // 生成マスタ（JSON原文＋配置ノイズPNG）がワールド作成時と違えば台帳がサーバー正本とずれる。版・解像度と同じく例外で止める
            // If the generation master (JSON text + placement-noise PNGs) differs from world creation, the ledger drifts from the server's truth; fail as for version and resolution
            var selectedGeneration = MasterHolder.GenerationMaster.SelectedGeneration;
            var fingerprint = GenerationMasterFingerprint.Compute(MasterHolder.GenerationMaster.SourceJsonText, selectedGeneration, serverDataDirectory);
            terrainMeta.ThrowIfGenerationMasterFingerprintDiffers(fingerprint);

            // サーバーの唯一の入口と同じ2段（config組立→アルゴリズム選択→生成）を通る。手で組み直さない
            // ただしスポーン探索だけは再計算せず、ワールド作成時に確定した原点を注入して同じ窓を指させる
            // Go through the very two steps of the server's single entry (build config, pick algorithm, generate); never hand-assemble
            // The spawn search alone is not recomputed: the origins settled at world creation are injected so the same window is addressed
            var config = MapGenerationPipeline.BuildConfigWithSettledOrigins(
                selectedGeneration, terrainMeta.WorldSeed, serverDataDirectory, terrainMeta.Origins);
            terrainMeta.ThrowIfTerrainResolutionDiffers(config.Resolution);

            // pass-1: サーバーと同じ生成を丸ごと回し、配置台帳（クラスタ・種別込み）を得る。高さは捨てて転送値を正本にする
            // pass-1: run the very same generation to obtain the placement ledger (clusters and kinds); its heights are dropped in favour of the transferred ones
            var run = MapGenerationPipeline.Generate(selectedGeneration, config);

            // 注入が効いていれば原点は構造的に一致する。崩れた台帳は別の窓の配置なので、台帳を取る前に止める
            // The injection makes the origins agree structurally; a drifted ledger holds another window's placements, so stop before taking it
            terrainMeta.ThrowIfOriginsDiffer(run.Output.NoiseOrigin, run.Output.SceneOrigin);

            // 高さ源は共有キャッシュ。組み立てはサーバー先焼きと共有し、Configは生成が実際に使ったものを渡す
            // The height source is the shared cache; assembly is shared with the server prebake, and the config handed over is the one the run actually used
            var heightSource = WorldDataDirectory.ForWorldCache(terrainMeta.WorldId);
            var factoryResult = TileVisualBakerFactory.Create(run.Config, terrainMeta, run.Ledger, heightSource, selectedGeneration);
            var baker = factoryResult.Baker;
            var gridConfig = factoryResult.GridConfig;
            var layout = WorldTerrainLayout.CreateTileMaps(
                TerrainTransferMeta.EnumerateTileCoordinates(terrainMeta.TerrainTileCount),
                new Vector3(gridConfig.terrainWidth, gridConfig.terrainHeight, gridConfig.terrainLength), gridConfig.Resolution,
                factoryResult.OrderedLayerAddresses, baker.DetailPrototypes);
            return new WorldTerrainSession(layout, baker);
        }

        public BakedTerrainTile BakeTile(int tileX, int tileZ)
        {
            if (Layout.Kind != TerrainLayoutKind.TileMaps)
                throw new InvalidOperationException("[WorldTerrainSession] An authored terrain owns no tile to bake.");
            return _baker.Bake(tileX, tileZ);
        }
    }
}
