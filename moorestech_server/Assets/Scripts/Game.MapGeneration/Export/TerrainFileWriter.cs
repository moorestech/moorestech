using System;
using System.IO;
using Game.MapGeneration.Pipeline;
using Game.Paths;
using UnityEngine;

namespace Game.MapGeneration.Export
{
    // 生成出力をterrainバイナリとcache READMEへ書き出す
    // Writes generation output to terrain binaries and the cache README
    public static class TerrainFileWriter
    {
        private const string CacheReadmeText = "このディレクトリは削除可能です。削除しても次回起動時に自動で再構築されます。";

        public static void Write(WorldDataDirectory worldDataDirectory, MapGenerationOutput output)
        {
            Directory.CreateDirectory(worldDataDirectory.TerrainDirectory);
            Directory.CreateDirectory(worldDataDirectory.CacheDirectory);

            // 全タイルのheightを書き出す。ファイル名の格子indexは転送層のEnumerateTileCoordinatesと同じ
            // Write every tile's height; grid indices in filenames match the transfer layer's enumeration
            foreach (var tile in output.Tiles)
                WriteHeightFile(worldDataDirectory, tile, output.Resolution);
            File.WriteAllText(worldDataDirectory.CacheReadmeFilePath, CacheReadmeText);

            #region Internal

            static void WriteHeightFile(WorldDataDirectory worldDataDirectory, TerrainTileOutput tile, int resolution)
            {
                // 長さが解像度と食い違うと、読み側が別の行から読み始めて全画素が流れる。書く前に止める
                // A length disagreeing with the resolution would start the reader on another row and shift every pixel, so it stops before writing
                if (tile.Heights.Length != resolution * resolution)
                    throw new InvalidOperationException(
                        $"[TerrainFileWriter] Tile ({tile.TileX}, {tile.TileZ}) holds {tile.Heights.Length} heights for a {resolution}x{resolution} tile of {resolution * resolution} pixels.");

                // 0-1正規化高さをushortへ変換しリトルエンディアンで書き込む(r16フォーマット)。
                // ノイズの浮動小数点誤差で範囲外(例:1.0000003)になり得るためクランプ後に四捨五入する。
                // Convert normalized 0-1 height to ushort and write little-endian (r16 format).
                // Clamp before rounding: noise drift can push values slightly out of [0,1].
                var heightFilePath = worldDataDirectory.TerrainHeightFilePath(tile.TileX, tile.TileZ);
                var buffer = new byte[tile.Heights.Length * 2];
                for (var i = 0; i < tile.Heights.Length; i++)
                {
                    var clamped = Mathf.Clamp01(tile.Heights[i]);
                    var value = (ushort)Mathf.Clamp(Mathf.RoundToInt(clamped * ushort.MaxValue), 0, ushort.MaxValue);
                    buffer[i * 2] = (byte)(value & 0xFF);
                    buffer[i * 2 + 1] = (byte)(value >> 8);
                }
                File.WriteAllBytes(heightFilePath, buffer);
            }

            #endregion
        }
    }
}
