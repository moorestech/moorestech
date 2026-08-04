using System.IO;
using Game.MapGeneration.Pipeline;
using Game.Paths;
using UnityEngine;

namespace Game.MapGeneration.Export
{
    // 生成パイプライン出力をterrainバイナリ(height/biome)とcache READMEへ書き出す。
    // Writes pipeline output to terrain binaries (height/biome) and the cache README.
    public static class TerrainFileWriter
    {
        private const string CacheReadmeText = "このディレクトリは削除可能です。削除しても次回起動時に自動で再構築されます。";

        // 現在の生成は単一タイル(0,0)のみ出力する。WorldProvisionerのTerrainTileCount=1と対応する
        // Generation currently emits only the single tile (0,0), matching WorldProvisioner's TerrainTileCount = 1
        private const int SingleTileX = 0;
        private const int SingleTileZ = 0;

        public static void Write(WorldDataDirectory worldDataDirectory, MapGenerationOutput output)
        {
            Directory.CreateDirectory(worldDataDirectory.TerrainDirectory);
            Directory.CreateDirectory(worldDataDirectory.CacheDirectory);

            WriteHeightFile(worldDataDirectory, output);
            WriteBiomeFile(worldDataDirectory, output);
            File.WriteAllText(worldDataDirectory.CacheReadmeFilePath, CacheReadmeText);

            #region Internal

            static void WriteHeightFile(WorldDataDirectory worldDataDirectory, MapGenerationOutput output)
            {
                // 0-1正規化高さをushortへ変換しリトルエンディアンで書き込む(r16フォーマット)。
                // ノイズの浮動小数点誤差で範囲外(例:1.0000003)になり得るためクランプ後に四捨五入する。
                // Convert normalized 0-1 height to ushort and write little-endian (r16 format).
                // Clamp before rounding: noise drift can push values slightly out of [0,1].
                var heightFilePath = worldDataDirectory.TerrainHeightFilePath(SingleTileX, SingleTileZ);
                var buffer = new byte[output.Heights.Length * 2];
                for (var i = 0; i < output.Heights.Length; i++)
                {
                    var clamped = Mathf.Clamp01(output.Heights[i]);
                    var value = (ushort)Mathf.Clamp(Mathf.RoundToInt(clamped * ushort.MaxValue), 0, ushort.MaxValue);
                    buffer[i * 2] = (byte)(value & 0xFF);
                    buffer[i * 2 + 1] = (byte)(value >> 8);
                }
                File.WriteAllBytes(heightFilePath, buffer);
            }

            static void WriteBiomeFile(WorldDataDirectory worldDataDirectory, MapGenerationOutput output)
            {
                var biomeFilePath = worldDataDirectory.TerrainBiomeFilePath(SingleTileX, SingleTileZ);
                File.WriteAllBytes(biomeFilePath, output.BiomeIndices);
            }

            #endregion
        }
    }
}
