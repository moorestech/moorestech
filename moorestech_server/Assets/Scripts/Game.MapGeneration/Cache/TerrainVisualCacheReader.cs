using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using static Game.MapGeneration.Cache.TerrainVisualCacheFormat;

namespace Game.MapGeneration.Cache
{
    /// <summary>
    ///     書き出した見た目を、固定headerとpayloadチェックサムを検証してから読み戻す
    ///     Reads written visuals back only after verifying the fixed header and payload checksum
    /// </summary>
    public static class TerrainVisualCacheReader
    {
        public static bool TryRead(
            string filePath, string expectedCacheKey, int expectedAlphamapResolution, int expectedLayerCount,
            int expectedDetailResolution, int expectedDetailMapCount, out TerrainTileVisual tileVisual, out string brokenReason)
        {
            tileVisual = null;
            brokenReason = null;
            // cacheファイルはロック・削除・権限変更が起こり得る外部I/O境界なので、限定例外を取り逃しへ隔離する
            // Cache files form an external I/O boundary where locking, deletion, and permission changes can occur, so limited exceptions become misses
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return TryReadOpenedStream(stream, expectedCacheKey, expectedAlphamapResolution, expectedLayerCount,
                    expectedDetailResolution, expectedDetailMapCount, out tileVisual, out brokenReason);
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
            catch (IOException exception)
            {
                brokenReason = $"cache I/O failed: {exception.Message}";
                return false;
            }
            catch (UnauthorizedAccessException exception)
            {
                brokenReason = $"cache access was denied: {exception.Message}";
                return false;
            }
        }
        private static bool TryReadOpenedStream(
            FileStream stream, string expectedCacheKey, int expectedAlphamapResolution, int expectedLayerCount,
            int expectedDetailResolution, int expectedDetailMapCount, out TerrainTileVisual tileVisual, out string brokenReason)
        {
            tileVisual = null;
            brokenReason = null;
            if (stream.Length < HeaderByteLength)
            {
                brokenReason = $"file length {stream.Length} is shorter than the {HeaderByteLength}-byte header";
                return false;
            }

            var headerBytes = new byte[HeaderByteLength];
            if (!ReadExactly(stream, headerBytes))
            {
                brokenReason = "header ended before its declared fixed length";
                return false;
            }

            // 固定部分だけで形式・キー・宣言寸法を確認し、任意サイズのpayloadを読む前に信頼境界を作る
            // Validate format, key, and declared dimensions from fixed bytes before reading an arbitrarily sized payload
            if (ReadInt(headerBytes, 0) != MagicNumber || ReadInt(headerBytes, 4) != FormatVersion)
            {
                brokenReason = "magic number or format version is unsupported";
                return false;
            }
            if (Encoding.ASCII.GetString(headerBytes, 8, CacheKeyByteLength) != expectedCacheKey) return false;

            var alphamapResolution = ReadInt(headerBytes, AlphamapResolutionOffset);
            var layerCount = ReadInt(headerBytes, LayerCountOffset);
            var detailResolution = ReadInt(headerBytes, DetailResolutionOffset);
            var detailMapCount = ReadInt(headerBytes, DetailMapCountOffset);
            if (!HasSaneDimensions(alphamapResolution, layerCount, detailResolution, detailMapCount))
            {
                brokenReason = "declared dimensions exceed the visual cache format bounds";
                return false;
            }
            if (alphamapResolution != expectedAlphamapResolution || layerCount != expectedLayerCount ||
                detailMapCount != expectedDetailMapCount || (0 < detailMapCount && detailResolution != expectedDetailResolution))
            {
                brokenReason = "declared dimensions disagree with the expected terrain visual layout";
                return false;
            }

            if (!TryCalculatePayloadByteLength(alphamapResolution, layerCount, detailResolution, detailMapCount, out var payloadByteLength) ||
                stream.Length != HeaderByteLength + payloadByteLength)
            {
                brokenReason = "stream length disagrees with the checked declared payload length";
                return false;
            }

            // 固定バッファでpayloadを検算し、数百MiBのpayload複製を作らず復元へ進む
            // Validate the payload through a fixed buffer, avoiding a hundreds-of-MiB payload copy before reconstruction
            if (!MatchesPayloadChecksum(stream, headerBytes))
            {
                brokenReason = "payload checksum disagrees with the header";
                return false;
            }

            stream.Position = HeaderByteLength;
            tileVisual = new TerrainTileVisual(ReadAlphamap(), ReadDetailMaps());
            return true;

            #region Internal

            float[,,] ReadAlphamap()
            {
                var alphamap = new float[alphamapResolution, alphamapResolution, layerCount];
                var rowBytes = new byte[alphamapResolution * layerCount];
                for (var z = 0; z < alphamapResolution; z++)
                {
                    if (!ReadExactly(stream, rowBytes))
                        throw new EndOfStreamException("Alphamap payload changed while the cache was being read.");
                    var rowOffset = 0;
                    for (var x = 0; x < alphamapResolution; x++)
                    for (var layer = 0; layer < layerCount; layer++)
                        alphamap[z, x, layer] = rowBytes[rowOffset++] / WeightQuantizeScale;
                }

                return alphamap;
            }

            List<int[,]> ReadDetailMaps()
            {
                var detailMaps = new List<int[,]>(detailMapCount);
                var rowBytes = new byte[detailResolution * DetailBytesPerCell];
                for (var mapIndex = 0; mapIndex < detailMapCount; mapIndex++)
                {
                    var detailMap = new int[detailResolution, detailResolution];
                    for (var z = 0; z < detailResolution; z++)
                    {
                        if (!ReadExactly(stream, rowBytes))
                            throw new EndOfStreamException("Detail payload changed while the cache was being read.");
                        var rowOffset = 0;
                        for (var x = 0; x < detailResolution; x++)
                        {
                            detailMap[z, x] = rowBytes[rowOffset] | (rowBytes[rowOffset + 1] << 8);
                            rowOffset += DetailBytesPerCell;
                        }
                    }

                    detailMaps.Add(detailMap);
                }

                return detailMaps;
            }

            #endregion
        }

        private static bool HasSaneDimensions(int alphamapResolution, int layerCount, int detailResolution, int detailMapCount)
        {
            return 0 < alphamapResolution && alphamapResolution <= MaximumAlphamapResolution &&
                   0 < layerCount && layerCount <= MaximumLayerCount &&
                   0 <= detailResolution && detailResolution <= MaximumDetailResolution &&
                   0 <= detailMapCount && detailMapCount <= MaximumDetailMapCount &&
                   (detailMapCount == 0 || 0 < detailResolution);
        }

        private static bool ReadExactly(FileStream stream, byte[] bytes)
        {
            var offset = 0;
            while (offset < bytes.Length)
            {
                var bytesRead = stream.Read(bytes, offset, bytes.Length - offset);
                if (bytesRead == 0) return false;
                offset += bytesRead;
            }

            return true;
        }

        private static bool MatchesPayloadChecksum(FileStream stream, byte[] headerBytes)
        {
            const int checksumBufferByteLength = 1024 * 1024;
            using var payloadHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[checksumBufferByteLength];
            while (stream.Position < stream.Length)
            {
                var bytesRead = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, stream.Length - stream.Position));
                if (bytesRead == 0) return false;
                payloadHash.AppendData(buffer, 0, bytesRead);
            }

            var checksum = payloadHash.GetHashAndReset();
            for (var index = 0; index < PayloadChecksumByteLength; index++)
                if (headerBytes[PayloadChecksumOffset + index] != checksum[index]) return false;

            return true;
        }
    }
}
