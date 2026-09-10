using System;
using System.IO;
using System.Security.Cryptography;
using Game.Paths;

namespace Game.MapGeneration.Transfer
{
    // terrain実ファイル群を1本の論理ストリームとみなしたSHA256。配信側と復元側が同じ実装で照合するため転送層に置く
    // SHA256 over the terrain files viewed as one logical stream; it lives in the transfer layer so sender and restorer compare with one implementation
    public static class TerrainStreamHasher
    {
        // terrain実ファイルが真実源なので毎回実体から計算する。保存もキャッシュもしない(差し替え・再生成で乖離するため)
        // The real terrain files are the source of truth, so recompute every time; never persist or cache (it would drift)
        // メタは呼び出し側が読んだものを受け取る。同一応答のチャンク総数とハッシュが別スナップショットを指さないようにするため
        // The caller passes in the meta it already read, so a single response never mixes two terrain snapshots
        public static string Compute(WorldDataDirectory worldDataDirectory, TerrainTransferMeta terrainMeta)
        {
            // 地形が無いワールドはハッシュ対象が存在しない。空文字が「地形なし」の表明になる
            // A terrain-less world has nothing to hash; the empty string states "no terrain"
            if (terrainMeta is not GeneratedTerrainTransferMeta generatedMeta) return string.Empty;

            generatedMeta.ThrowIfOwnsNoChunk();

            using var sha256 = SHA256.Create();
            foreach (var filePath in TerrainTransferMeta.EnumerateStreamFilePaths(worldDataDirectory, generatedMeta.TerrainTileCount))
            {
                var fileBytes = File.ReadAllBytes(filePath);
                sha256.TransformBlock(fileBytes, 0, fileBytes.Length, null, 0);
            }
            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            return BitConverter.ToString(sha256.Hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
