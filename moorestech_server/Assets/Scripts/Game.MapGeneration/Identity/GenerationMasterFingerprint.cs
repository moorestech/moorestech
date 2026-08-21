using System.IO;
using System.Security.Cryptography;
using System.Text;
using Game.MapGeneration.Pipeline.Runtime;
using Mooresmaster.Model.GenerationModule;

namespace Game.MapGeneration.Identity
{
    // 生成マスタの指紋。JSON原文と、treePlacement の texturePngPath が指す全PNGのバイト列を連結した SHA256
    // The generation master's fingerprint: SHA256 over the JSON text plus the bytes of every PNG the treePlacement texturePngPaths point at
    public static class GenerationMasterFingerprint
    {
        public static string Compute(string generationMasterJsonText, Generation selected, string serverDataDirectory)
        {
            using var sha256 = SHA256.Create();
            var textBytes = Encoding.UTF8.GetBytes(generationMasterJsonText);
            sha256.TransformBlock(textBytes, 0, textBytes.Length, null, 0);

            // PNG の列挙は PlacementNoiseTextureResolver と同じ走査（全バイオームの treePlacement.prototypes の4ノイズ）。空パスは読まない
            // PNG enumeration mirrors PlacementNoiseTextureResolver (the four noises of every biome's treePlacement prototypes); empty paths are skipped
            foreach (var pngPath in PlacementNoiseTextureResolver.EnumerateTexturePngPaths(GenerationRuntimeConfigFactory.Build(selected)))
            {
                var pngBytes = File.ReadAllBytes(Path.Combine(serverDataDirectory, pngPath));
                sha256.TransformBlock(pngBytes, 0, pngBytes.Length, null, 0);
            }

            sha256.TransformFinalBlock(new byte[0], 0, 0);
            return BitConverterHex(sha256.Hash);
        }

        private static string BitConverterHex(byte[] hash)
        {
            return System.BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
