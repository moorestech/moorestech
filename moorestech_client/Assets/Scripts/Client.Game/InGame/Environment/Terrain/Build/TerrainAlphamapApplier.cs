using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build
{
    public static class TerrainAlphamapApplier
    {
        private const int RowsPerFrame = 64;

        public static async UniTask ApplyAsync(TerrainData terrainData, float[,,] alphamap)
        {
            var rowCount = alphamap.GetLength(0);
            var columnCount = alphamap.GetLength(1);
            var layerCount = alphamap.GetLength(2);
            for (var rowOffset = 0; rowOffset < rowCount; rowOffset += RowsPerFrame)
            {
                // 連続する行だけを切り出し、SetAlphamapsの単発停止時間を制限する
                // Slice contiguous rows to cap the duration of each individual SetAlphamaps stall
                var applyRowCount = Math.Min(RowsPerFrame, rowCount - rowOffset);
                var frameAlphamap = new float[applyRowCount, columnCount, layerCount];
                Buffer.BlockCopy(
                    alphamap, rowOffset * columnCount * layerCount * sizeof(float),
                    frameAlphamap, 0, applyRowCount * columnCount * layerCount * sizeof(float));
                terrainData.SetAlphamaps(0, rowOffset, frameAlphamap);

                // 最終チャンク以外はロード画面へ描画機会を返す
                // Return a rendering opportunity to the loading screen between non-final chunks
                if (rowOffset + applyRowCount < rowCount) await UniTask.Yield();
            }
        }
    }
}
