using System;
using System.IO;
using Game.Paths;

namespace Game.MapGeneration.Cache
{
    /// <summary>
    ///     height r16バイナリを2次元配列へ復元（TerrainFileWriterの逆変換）
    ///     Restores a height r16 binary into a 2D array (the inverse of TerrainFileWriter)
    /// </summary>
    public static class HeightFileLoader
    {
        // r16の1画素はushort。writerが乗じた65535で割って0-1の正規化高さへ戻す
        // One r16 pixel is a ushort; divide by the 65535 the writer multiplied by to recover the normalized 0-1 height
        private const float HeightNormalizeDivisor = ushort.MaxValue;

        private const int HeightBytesPerPixel = 2;

        // 戻り値は[z, x]。TerrainData.SetHeightsの[y, x]規約とwriterのz行→x列の並びに合わせる
        // Returns [z, x], matching TerrainData.SetHeights' [y, x] convention and the writer's row-z, column-x order
        public static float[,] LoadHeights(WorldDataDirectory worldDataDirectory, int tileX, int tileZ, int terrainResolution)
        {
            var filePath = worldDataDirectory.TerrainHeightFilePath(tileX, tileZ);
            var bytes = ReadWithExpectedLength(filePath, terrainResolution, HeightBytesPerPixel);

            var heights = new float[terrainResolution, terrainResolution];
            for (var z = 0; z < terrainResolution; z++)
            for (var x = 0; x < terrainResolution; x++)
            {
                // writerが下位バイト先行で書いた2バイトをリトルエンディアンのushortとして組み立てる
                // Reassemble the two bytes the writer emitted low-byte-first as a little-endian ushort
                var byteOffset = (z * terrainResolution + x) * HeightBytesPerPixel;
                var quantizedHeight = (ushort)(bytes[byteOffset] | (bytes[byteOffset + 1] << 8));
                heights[z, x] = quantizedHeight / HeightNormalizeDivisor;
            }

            return heights;

            #region Internal

            // 長さ不一致のまま読むと以降の全画素が1列ずつずれる。切り詰めや解像度取り違えは明示失敗にする
            // Reading a mismatched length shifts every later pixel by a column, so truncation or a wrong resolution fails loudly
            static byte[] ReadWithExpectedLength(string filePath, int terrainResolution, int bytesPerPixel)
            {
                var expectedByteLength = terrainResolution * terrainResolution * bytesPerPixel;
                var bytes = File.ReadAllBytes(filePath);
                if (bytes.Length != expectedByteLength)
                    throw new InvalidOperationException(
                        $"Terrain file '{filePath}' holds {bytes.Length} bytes but resolution {terrainResolution} requires {expectedByteLength}.");

                return bytes;
            }

            #endregion
        }
    }
}
