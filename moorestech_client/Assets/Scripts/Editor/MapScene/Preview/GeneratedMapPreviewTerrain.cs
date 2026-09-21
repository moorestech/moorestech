#if UNITY_EDITOR
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Environment.Terrain.Assets;
using Client.Game.InGame.Environment.Terrain.Build;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Facade;
using UnityEngine;

namespace Client.MapScene.Editor
{
    public static class GeneratedMapPreviewTerrain
    {
        public static async UniTask BuildAsync(TiledTerrainSession session, GeneratedMapPreviewContent content,
            ITerrainAssetLoader assets, CancellationToken cancellationToken)
        {
            // 実行時と同じ順序・描画設定を借用資産で組み立てる
            // Assemble the runtime ordering and render settings using borrowed assets
            var layout = session.Layout;
            var layers = await TerrainLayerAssetLoader.LoadAsync(layout.TextureLayerAddresses, assets, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var prototypes = await DetailPrototypeAssetResolver.ResolveAsync(layout.DetailPrototypes, assets, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var material = await TerrainMaterialAssetLoader.LoadAsync(assets, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var terrains = new Dictionary<Vector2Int, Terrain>();

            // 全タイルの結果をそのまま載せ、await後に寿命を再確認する
            // Mount every tile result unchanged and recheck lifetime after each await
            foreach (var (tileX, tileZ) in layout.TileCoordinates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tile = session.BakeTile(tileX, tileZ);
                var data = content.CreateTerrainData();
                await TerrainDataAssembler.AssembleIntoAsync(data, layout, tile, prototypes, layers, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var terrain = TerrainObjectFactory.Create(content.Root, $"Terrain_{tileX}_{tileZ}",
                    tile.ScenePosition, data, material, layout.DetailObjectDistance, layout.DetailObjectDensity);
                terrains.Add(new Vector2Int(tileX, tileZ), terrain);
            }
            TerrainNeighborLinker.Link(terrains);
        }
    }
}
#endif
