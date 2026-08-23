using System;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Facade;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build
{
    /// <summary>
    ///     RGBA8平面をTerrainへ直接適用
    ///     SetAlphamapsはfloat[z,x,layer]を要求するため、平面から組み直す変換とUnity側の再量子化が二重にかかる。
    ///     平面はUnityがalphamapを保持している形そのものなので、テクスチャへ直接載せれば変換が1つも要らない。
    ///     Applies RGBA8 planes directly to Terrain.
    ///     SetAlphamaps demands a float[z, x, layer], which costs both a rebuild from the planes and Unity's own requantization.
    ///     The planes are exactly how Unity already holds an alphamap, so loading them into the textures needs no conversion at all.
    /// </summary>
    public static class TerrainAlphamapApplier
    {
        public static async UniTask ApplyAsync(
            TerrainData terrainData, TerrainLayer[] terrainLayers, BakedTerrainTile tile)
        {
            var alphamap = tile.Alphamap;
            if (alphamap == null) return;

            // レイヤー表はUnityの割り当て前に照合し、食い違い時に既定のTerrainDataを一切変えない
            // Check layer tables before assigning Unity data so a mismatch leaves its defaults entirely unchanged
            if (terrainLayers.Length != alphamap.LayerCount)
                throw new InvalidOperationException(
                    $"[TerrainAlphamapApplier] {terrainLayers.Length} terrain layers were resolved but the tile was baked for {alphamap.LayerCount}.");

            // Unityは解像度とレイヤー表から平面テクスチャを確保するため、アップロード前にこの順で設定する
            // Unity allocates plane textures from resolution and layers, so set them in this order before uploading
            terrainData.alphamapResolution = alphamap.Resolution;
            terrainData.terrainLayers = terrainLayers;
            var alphamapTextures = terrainData.alphamapTextures;

            for (var planeIndex = 0; planeIndex < alphamapTextures.Length; planeIndex++)
            {
                // Unity APIはmutable配列だけを受けるため、実アップロード時に限って防御コピーを作る
                // Unity accepts only a mutable array, so make the defensive copy only for the actual upload
                alphamapTextures[planeIndex].SetPixelData(alphamap.Planes[planeIndex].ToArray(), 0);
                alphamapTextures[planeIndex].Apply(false);

                // 平面ごとに描画機会を返し、巨大なタイルでもロード画面を占有し続けない
                // Yield after each plane so even a large tile does not keep the loading screen occupied
                await UniTask.Yield();
            }

            // テクスチャを直接書いた事実はTerrain側へ伝える必要がある。伝えないとbasemapと衝突判定が古い重みのまま残る
            // Writing the textures directly must be announced to the terrain, or its basemap and collision keep the old weights
            terrainData.DirtyTextureRegion(
                TerrainData.AlphamapTextureName, new RectInt(0, 0, alphamap.Resolution, alphamap.Resolution), false);
        }
    }
}
