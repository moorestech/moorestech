using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using static Client.Game.InGame.Environment.Terrain.Visual.Cache.TerrainVisualCacheFormat;

namespace Client.Game.InGame.Environment.Terrain.Visual.Cache
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

            var payloadByteLength = (long)alphamapResolution * alphamapResolution * layerCount +
                                    (long)detailMapCount * detailResolution * detailResolution * DetailBytesPerCell;
            if (MaximumPayloadByteLength < payloadByteLength || stream.Length != HeaderByteLength + payloadByteLength)
            {
                brokenReason = "stream length disagrees with the bounded declared payload length";
                return false;
            }

            // サイズ検査済みpayloadだけを読み、復元より先に書き込み時のSHA-256と照合する
            // Read only the size-validated payload and compare its write-time SHA-256 before reconstruction
            var payloadBytes = new byte[(int)payloadByteLength];
            if (!ReadExactly(stream, payloadBytes))
            {
                brokenReason = "payload ended before its validated length";
                return false;
            }
            if (!MatchesPayloadChecksum(headerBytes, payloadBytes))
            {
                brokenReason = "payload checksum disagrees with the header";
                return false;
            }

            var readOffset = 0;
            tileVisual = new TerrainTileVisual(ReadAlphamap(), ReadDetailMaps());
            return true;

            #region Internal

            float[,,] ReadAlphamap()
            {
                var alphamap = new float[alphamapResolution, alphamapResolution, layerCount];
                for (var z = 0; z < alphamapResolution; z++)
                for (var x = 0; x < alphamapResolution; x++)
                for (var layer = 0; layer < layerCount; layer++)
                    alphamap[z, x, layer] = payloadBytes[readOffset++] / WeightQuantizeScale;

                return alphamap;
            }

            List<int[,]> ReadDetailMaps()
            {
                var detailMaps = new List<int[,]>(detailMapCount);
                for (var mapIndex = 0; mapIndex < detailMapCount; mapIndex++)
                {
                    var detailMap = new int[detailResolution, detailResolution];
                    for (var z = 0; z < detailResolution; z++)
                    for (var x = 0; x < detailResolution; x++)
                    {
                        detailMap[z, x] = payloadBytes[readOffset] | (payloadBytes[readOffset + 1] << 8);
                        readOffset += DetailBytesPerCell;
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

        private static bool MatchesPayloadChecksum(byte[] headerBytes, byte[] payloadBytes)
        {
            using var sha256 = SHA256.Create();
            var checksum = sha256.ComputeHash(payloadBytes);
            for (var index = 0; index < PayloadChecksumByteLength; index++)
                if (headerBytes[PayloadChecksumOffset + index] != checksum[index]) return false;

            return true;
        }
    }
}
