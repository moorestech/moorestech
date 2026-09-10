using System;
using System.IO;
using System.IO.Compression;
using Game.MapGeneration.Transfer;
using Game.Paths;

namespace Server.Protocol.PacketResponse.MapData
{
    // 地形群を論理列として圧縮分割する
    // Compress and split terrain as one stream
    public static class TerrainChunkReader
    {
        public static byte[] Read(WorldDataDirectory worldDataDirectory, int chunkIndex)
        {
            var terrainMeta = TerrainTransferMetaReader.Read(worldDataDirectory);

            // 地形を持たないワールドへのチャンク要求は空応答で誤魔化さず例外にする
            // A chunk request against a terrain-less world throws instead of being masked by an empty response
            if (terrainMeta is not GeneratedTerrainTransferMeta generatedMeta)
                throw new InvalidOperationException($"World in '{terrainMeta.MapMode}' mode owns no terrain chunk to read.");

            generatedMeta.ThrowIfOwnsNoChunk();
            if (chunkIndex < 0 || generatedMeta.TerrainChunkTotal <= chunkIndex)
                throw new ArgumentOutOfRangeException(nameof(chunkIndex),
                    $"ChunkIndex {chunkIndex} is out of range 0..{generatedMeta.TerrainChunkTotal - 1}.");

            var sliceStartOffset = (long)chunkIndex * TerrainTransferMeta.ChunkByteSize;
            var sliceBytes = ReadStreamRange(worldDataDirectory, generatedMeta.TerrainTileCount, sliceStartOffset);
            return Compress(sliceBytes);
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
