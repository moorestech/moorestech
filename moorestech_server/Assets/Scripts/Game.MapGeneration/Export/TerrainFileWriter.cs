using System.IO;
using Game.MapGeneration.Pipeline;
using Game.Paths;

namespace Game.MapGeneration.Export
{
    // 生成パイプライン出力をterrainバイナリ(height/biome)とcache READMEへ書き出す。
    // Writes pipeline output to terrain binaries (height/biome) and the cache README.
    public static class TerrainFileWriter
    {
        private const string CacheReadmeText = "このディレクトリは削除可能です。削除しても次回起動時に自動で再構築されます。";

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
                // Convert normalized 0-1 height to ushort and write little-endian (r16 format).
                var heightFilePath = Path.Combine(worldDataDirectory.TerrainDirectory, "height_0_0.r16");
                var buffer = new byte[output.Heights.Length * 2];
                for (var i = 0; i < output.Heights.Length; i++)
                {
                    var normalized = output.Heights[i];
                    var value = (ushort)(normalized * ushort.MaxValue);
                    buffer[i * 2] = (byte)(value & 0xFF);
                    buffer[i * 2 + 1] = (byte)(value >> 8);
                }
                File.WriteAllBytes(heightFilePath, buffer);
            }

            static void WriteBiomeFile(WorldDataDirectory worldDataDirectory, MapGenerationOutput output)
            {
                var biomeFilePath = Path.Combine(worldDataDirectory.TerrainDirectory, "biome_0_0.bin");
                File.WriteAllBytes(biomeFilePath, output.BiomeIndices);
            }

            #endregion
        }
    }
}
