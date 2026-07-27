using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using Game.MapGeneration.Transfer;
using Game.Paths;

namespace Server.Protocol.PacketResponse.MapData
{
    // terrain実ファイル群を1本の論理ストリームとして扱い、GZip圧縮したチャンク断片と全体ハッシュを返す
    // Treats the terrain files as one logical stream, returning GZip-compressed chunk slices and the whole-stream hash
    public static class TerrainChunkReader
    {
        public static byte[] Read(WorldDataDirectory worldDataDirectory, int chunkIndex)
        {
            var terrainMeta = TerrainTransferMetaReader.Read(worldDataDirectory);

            // 地形を持たないワールドへのチャンク要求は空応答で誤魔化さず例外にする
            // A chunk request against a terrain-less world throws instead of being masked by an empty response
            if (terrainMeta.TerrainChunkTotal <= 0)
                throw new InvalidOperationException($"World in '{terrainMeta.MapMode}' mode owns no terrain chunk to read.");
            if (chunkIndex < 0 || terrainMeta.TerrainChunkTotal <= chunkIndex)
                throw new ArgumentOutOfRangeException(nameof(chunkIndex),
                    $"ChunkIndex {chunkIndex} is out of range 0..{terrainMeta.TerrainChunkTotal - 1}.");

            var sliceStartOffset = (long)chunkIndex * TerrainTransferMeta.ChunkByteSize;
            var sliceBytes = ReadStreamRange(worldDataDirectory, terrainMeta.TerrainTileCount, sliceStartOffset);
            return Compress(sliceBytes);
        }

        // terrain実ファイルが真実源なので毎回実体から計算する。保存もキャッシュもしない(差し替え・再生成で乖離するため)
        // The real terrain files are the source of truth, so recompute every time; never persist or cache (it would drift)
        public static string ComputeStreamHash(WorldDataDirectory worldDataDirectory)
        {
            var terrainMeta = TerrainTransferMetaReader.Read(worldDataDirectory);

            // 地形が無いワールドはハッシュ対象が存在しない。空文字が「地形なし」の表明になる
            // A terrain-less world has nothing to hash; the empty string states "no terrain"
            if (terrainMeta.TerrainChunkTotal <= 0) return string.Empty;

            using var sha256 = SHA256.Create();
            foreach (var filePath in TerrainTransferMeta.EnumerateStreamFilePaths(worldDataDirectory, terrainMeta.TerrainTileCount))
            {
                var fileBytes = File.ReadAllBytes(filePath);
                sha256.TransformBlock(fileBytes, 0, fileBytes.Length, null, 0);
            }
            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            return BitConverter.ToString(sha256.Hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        // 論理ストリーム上の[startOffset, startOffset+ChunkByteSize)をファイル境界をまたいで切り出す
        // Slice [startOffset, startOffset+ChunkByteSize) out of the logical stream, crossing file boundaries
        private static byte[] ReadStreamRange(WorldDataDirectory worldDataDirectory, int terrainTileCount, long startOffset)
        {
            var sliceEndOffset = startOffset + TerrainTransferMeta.ChunkByteSize;
            using var sliceStream = new MemoryStream();

            var fileStartOffset = 0L;
            foreach (var filePath in TerrainTransferMeta.EnumerateStreamFilePaths(worldDataDirectory, terrainTileCount))
            {
                var fileEndOffset = fileStartOffset + new FileInfo(filePath).Length;

                // 要求範囲と重なるファイルだけを、その重なり部分だけ読む
                // Read only the overlapping part, and only from files that overlap the requested range
                var overlapStartOffset = Math.Max(startOffset, fileStartOffset);
                var overlapEndOffset = Math.Min(sliceEndOffset, fileEndOffset);
                if (overlapStartOffset < overlapEndOffset)
                    AppendFileRange(filePath, overlapStartOffset - fileStartOffset, (int)(overlapEndOffset - overlapStartOffset), sliceStream);

                fileStartOffset = fileEndOffset;
                if (sliceEndOffset <= fileStartOffset) break;
            }

            return sliceStream.ToArray();
        }

        private static void AppendFileRange(string filePath, long fileOffset, int length, MemoryStream destination)
        {
            using var fileStream = File.OpenRead(filePath);
            fileStream.Seek(fileOffset, SeekOrigin.Begin);

            // FileStream.Readは要求長より短く返しうるので読み切るまで回す
            // FileStream.Read may return fewer bytes than requested, so loop until the range is filled
            var buffer = new byte[length];
            var readTotal = 0;
            while (readTotal < length)
            {
                var readLength = fileStream.Read(buffer, readTotal, length - readTotal);
                if (readLength == 0) throw new EndOfStreamException($"Terrain file '{filePath}' ended before the requested range.");
                readTotal += readLength;
            }

            destination.Write(buffer, 0, length);
        }

        private static byte[] Compress(byte[] rawBytes)
        {
            using var compressedStream = new MemoryStream();
            using (var gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal))
                gzipStream.Write(rawBytes, 0, rawBytes.Length);
            return compressedStream.ToArray();
        }
    }
}
