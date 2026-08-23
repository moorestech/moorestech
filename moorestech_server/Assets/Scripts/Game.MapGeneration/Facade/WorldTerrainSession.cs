using Core.Master;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Transfer;
using UnityEngine;

namespace Game.MapGeneration.Facade
{
    /// <summary>
    ///     生成システムが外へ見せる唯一の入口。ワールド同一性（転送メタ）を受け取り、結果だけを返す。
    ///     実際に生成したのか固定の地形を返しただけなのか、キャッシュを引いたのかは外から区別できない
    ///     The single entry the generation system exposes: takes the world identity (transfer meta) and returns results only.
    ///     Whether it generated, returned an authored terrain, or hit a cache is indistinguishable from outside
    /// </summary>
    public abstract class WorldTerrainSession
    {
        public WorldTerrainLayout Layout { get; }

        protected WorldTerrainSession(WorldTerrainLayout layout)
        {
            Layout = layout;
        }

        public static WorldTerrainSession Open(TerrainTransferMeta terrainMeta, string serverDataDirectory)
        {
            // オーサリング済み地形は焼くタイルを持たない。焼ける口はTiledTerrainSessionにしか無く、判別子とnullで二重に持たない
            // An authored terrain owns no tile to bake; only TiledTerrainSession exposes baking, so no discriminator-plus-null pair states it twice
            if (terrainMeta.IsTemplate) return new AuthoredTerrainSession(WorldTerrainLayout.CreateTerrainAsset());

            // 転送メタは別ビルドのサーバーからも届く。転送ファイル構成の版が違えばこの先の読み出しが全部ずれるので冒頭で止める
            // The meta can arrive from a server on another build; a differing transfer-layout version skews every read below, so stop at the head
            terrainMeta.ThrowIfGeneratorVersionDiffers();

            // 生成マスタ（JSON原文＋配置ノイズPNG）がワールド作成時と違えば台帳がサーバー正本とずれる。版・解像度と同じく例外で止める
            // If the generation master (JSON text + placement-noise PNGs) differs from world creation, the ledger drifts from the server's truth; fail as for version and resolution
            terrainMeta.ThrowIfGenerationMasterDiffers(serverDataDirectory);

            // サーバーの唯一の入口と同じconfig組立を通す。手で組み直さない
            // ただしスポーン探索だけは再計算せず、ワールド作成時に確定した原点を注入して同じ窓を指させる
            // Go through the very config assembly of the server's single entry; never hand-assemble
            // The spawn search alone is not recomputed: the origins settled at world creation are injected so the same window is addressed
            var selectedGeneration = MasterHolder.GenerationMaster.SelectedGeneration;
            var config = MapGenerationPipeline.BuildConfigWithSettledOrigins(
                selectedGeneration, terrainMeta.WorldSeed, serverDataDirectory, terrainMeta.Origins);
            terrainMeta.ThrowIfTerrainResolutionDiffers(config.Resolution);

            // 原点は格子の寸法と注入したGだけで決まり、生成を回さなくても確かめられる
            // 崩れた原点は別の窓を指しているので、その窓で焼いた見た目をキャッシュへ書き込む前に止める
            // The origins follow from the grid dimensions and the injected G alone, so they are checkable without running generation
            // Shifted origins address another window, so stop before visuals baked on that window can reach the cache
            var sceneOrigin = config.TileScenePosition(0, 0);
            var noiseOrigin = new Vector2(config.worldOffsetX, config.worldOffsetZ) + sceneOrigin;
            terrainMeta.ThrowIfOriginsDiffer(noiseOrigin, sceneOrigin);

            // 配置台帳（pass-1）はキャッシュを取り逃したタイルが出て初めて要る。鍵は転送メタの指紋で足りる
            // The ledger (pass-1) is needed only once a tile misses the cache; the transfer meta's digest suffices for the key
            var ledgerSource = new RegeneratedPlacementLedgerSource(selectedGeneration, config);

            // 組み立てはサーバー先焼きと共有する。高さ源の決定はfactoryが持つ
            // The assembly is shared with the server prebake; the factory owns the height-source decision
            var factoryResult = TileVisualBakerFactory.CreateForClient(config, terrainMeta, ledgerSource, selectedGeneration);
            var gridConfig = factoryResult.GridConfig;

            // 生成内部のdetail設定は境界を越えない。並びを保ったまま公開仕様へ写す
            // The generation-internal detail configs never cross the boundary; they are copied into the public specs with their order intact
            var layout = WorldTerrainLayout.CreateTileMaps(
                TerrainTransferMeta.EnumerateTileCoordinates(terrainMeta.TerrainTileCount),
                new Vector3(gridConfig.terrainWidth, gridConfig.terrainHeight, gridConfig.terrainLength), gridConfig.Resolution,
                factoryResult.OrderedLayerAddresses, DetailPrototypeSpecCollector.Collect(factoryResult.Baker.DetailPrototypes));
            return new TiledTerrainSession(layout, factoryResult.Baker);
        }
    }
}
