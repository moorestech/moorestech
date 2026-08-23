using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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
            TerrainData terrainData, IReadOnlyList<ReadOnlyMemory<byte>> alphamapPlanes, int alphamapResolution)
        {
            var alphamapTextures = terrainData.alphamapTextures;

            // 平面数はレイヤー数から決まる。食い違うのは焼き手と適用側でレイヤー表が違うときで、黙って埋めると別の層が塗られる
            // The plane count follows from the layer count; a disagreement means the baker and this side hold different layer tables, and filling the gap silently would paint another layer
            if (alphamapTextures.Length != alphamapPlanes.Count)
                throw new InvalidOperationException(
                    $"[TerrainAlphamapApplier] The terrain owns {alphamapTextures.Length} alphamap textures but {alphamapPlanes.Count} planes were baked.");

            for (var planeIndex = 0; planeIndex < alphamapTextures.Length; planeIndex++)
            {
                // Unity APIはmutable配列だけを受けるため、実アップロード時に限って防御コピーを作る
                // Unity accepts only a mutable array, so make the defensive copy only for the actual upload
                alphamapTextures[planeIndex].SetPixelData(alphamapPlanes[planeIndex].ToArray(), 0);
                alphamapTextures[planeIndex].Apply(false);

                // 最終平面以外はロード画面へ描画機会を返す
                // Return a rendering opportunity to the loading screen between non-final planes
                if (planeIndex + 1 < alphamapTextures.Length) await UniTask.Yield();
            }

            // テクスチャを直接書いた事実はTerrain側へ伝える必要がある。伝えないとbasemapと衝突判定が古い重みのまま残る
            // Writing the textures directly must be announced to the terrain, or its basemap and collision keep the old weights
            terrainData.DirtyTextureRegion(
                TerrainData.AlphamapTextureName, new RectInt(0, 0, alphamapResolution, alphamapResolution), false);
        }
    }
}
