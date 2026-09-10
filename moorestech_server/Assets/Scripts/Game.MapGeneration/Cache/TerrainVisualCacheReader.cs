using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Game.MapGeneration.Transfer;
using static Game.MapGeneration.Cache.TerrainVisualCacheFormat;

namespace Game.MapGeneration.Cache
{
    /// <summary>
    ///     書き出した見た目を、固定headerとpayloadチェックサムを検証してから読み戻す
    ///     payloadは1度しか読まない。復元先へ直接読み込みながら同じ1周でSHAを回し、検算のための2周目を作らない
    ///     Reads written visuals back only after verifying the fixed header and payload checksum
    ///     The payload is read exactly once: the SHA runs over the very pass that fills the destinations, so verification needs no second sweep
    /// </summary>
    public static class TerrainVisualCacheReader
    {
        public static bool TryRead(
            string filePath, string expectedCacheKey, int expectedHeightmapResolution, int expectedAlphamapResolution,
            int expectedLayerCount, int expectedDetailResolution, int expectedDetailMapCount,
            out TerrainTileVisual tileVisual, out string brokenReason)
        {
            tileVisual = null;
            brokenReason = null;
            // まだ焼いていないタイルもワールドごと消えたキャッシュも、痕跡を残さない取り逃しとして扱う
            // A tile not baked yet and a cache directory wiped with its world alike become misses that leave no trace
            if (!File.Exists(filePath)) return false;

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return TryReadOpenedStream(stream, expectedCacheKey, expectedHeightmapResolution, expectedAlphamapResolution,
                expectedLayerCount, expectedDetailResolution, expectedDetailMapCount, out tileVisual, out brokenReason);
        }

        private static bool TryReadOpenedStream(
            FileStream stream, string expectedCacheKey, int expectedHeightmapResolution, int expectedAlphamapResolution,
            int expectedLayerCount, int expectedDetailResolution, int expectedDetailMapCount,
            out TerrainTileVisual tileVisual, out string brokenReason)
        {
            tileVisual = null;
            brokenReason = null;
            if (stream.Length < HeaderByteLength)
            {
                brokenReason = $"file length {stream.Length} is shorter than the {HeaderByteLength}-byte header";
                return false;
            }

            var headerBytes = new byte[HeaderByteLength];
            if (!ReadExactly(stream, headerBytes, headerBytes.Length))
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

            var heightmapResolution = ReadInt(headerBytes, HeightmapResolutionOffset);
            var alphamapResolution = ReadInt(headerBytes, AlphamapResolutionOffset);
            var layerCount = ReadInt(headerBytes, LayerCountOffset);
            var detailResolution = ReadInt(headerBytes, DetailResolutionOffset);
            var detailMapCount = ReadInt(headerBytes, DetailMapCountOffset);
            if (!TryCalculatePayloadByteLength(heightmapResolution, alphamapResolution, layerCount, detailResolution,
                    detailMapCount, out var payloadByteLength))
            {
                brokenReason = "declared dimensions exceed the visual cache format bounds";
                return false;
            }
            if (heightmapResolution != expectedHeightmapResolution || alphamapResolution != expectedAlphamapResolution ||
                layerCount != expectedLayerCount || detailMapCount != expectedDetailMapCount ||
                (0 < detailMapCount && detailResolution != expectedDetailResolution))
            {
                brokenReason = "declared dimensions disagree with the expected terrain visual layout";
                return false;
            }
            if (stream.Length != HeaderByteLength + payloadByteLength)
            {
                brokenReason = "stream length disagrees with the checked declared payload length";
                return false;
            }

            // 復元しながらハッシュを積む。検算が合うまで結果を外へ出さないので、壊れたキャッシュが見た目へ流れることはない
            // The hash accumulates while restoring; nothing leaves this method until it matches, so a broken cache never reaches the visuals
            using var payloadHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            // 長さ検査後の短読は同時書き換えを示すため、破損として拒否する
            // A short read after the length check indicates a concurrent rewrite, so reject it as corruption
            if (!TryReadHeights(out var displayHeights) || !TryReadAlphamapPlanes(out var alphamapPlanes) ||
                !TryReadDetailMaps(out var detailMaps))
            {
                brokenReason = "payload ended before the length it declared and passed";
                return false;
            }

            var checksum = payloadHash.GetHashAndReset();
            for (var index = 0; index < PayloadChecksumByteLength; index++)
            {
                if (headerBytes[PayloadChecksumOffset + index] == checksum[index]) continue;
                brokenReason = "payload checksum disagrees with the header";
                return false;
            }

            var alphamap = TileAlphamap.CreateOwning(alphamapPlanes, alphamapResolution, layerCount);
            tileVisual = new TerrainTileVisual(displayHeights, alphamap, detailMaps);
            return true;

            #region Internal

            static bool ReadExactly(FileStream stream, byte[] bytes, int byteLength)
            {
                var offset = 0;
                while (offset < byteLength)
                {
                    var bytesRead = stream.Read(bytes, offset, byteLength - offset);
                    if (bytesRead == 0) return false;
                    offset += bytesRead;
                }

                return true;
            }

            bool TryReadHeights(out float[,] heights)
            {
                heights = new float[heightmapResolution, heightmapResolution];
                var rowBytes = new byte[heightmapResolution * HeightBytesPerPixel];
                for (var z = 0; z < heightmapResolution; z++)
                {
                    if (!ReadExactly(stream, rowBytes, rowBytes.Length)) return false;
                    payloadHash.AppendData(rowBytes);
                    var rowOffset = 0;
                    for (var x = 0; x < heightmapResolution; x++)
                    {
                        var quantizedHeight = (ushort)(rowBytes[rowOffset] | (rowBytes[rowOffset + 1] << 8));
                        heights[z, x] = quantizedHeight / HeightQuantizeScale;
                        rowOffset += HeightBytesPerPixel;
                    }
                }

                return true;
            }

            bool TryReadAlphamapPlanes(out byte[][] planes)
            {
                var planeByteLength = (int)AlphamapPlaneByteLength(alphamapResolution);
                planes = new byte[TileAlphamap.AlphamapPlaneCount(layerCount)][];
                for (var planeIndex = 0; planeIndex < planes.Length; planeIndex++)
                {
                    planes[planeIndex] = new byte[planeByteLength];
                    if (!ReadExactly(stream, planes[planeIndex], planeByteLength)) return false;
                    payloadHash.AppendData(planes[planeIndex]);
                }

                return true;
            }

            bool TryReadDetailMaps(out List<int[,]> detailMaps)
            {
                detailMaps = new List<int[,]>(detailMapCount);
                var rowBytes = new byte[detailResolution * DetailBytesPerCell];
                for (var mapIndex = 0; mapIndex < detailMapCount; mapIndex++)
                {
                    var detailMap = new int[detailResolution, detailResolution];
                    for (var z = 0; z < detailResolution; z++)
                    {
                        if (!ReadExactly(stream, rowBytes, rowBytes.Length)) return false;
                        payloadHash.AppendData(rowBytes);
                        var rowOffset = 0;
                        for (var x = 0; x < detailResolution; x++)
                        {
                            detailMap[z, x] = rowBytes[rowOffset] | (rowBytes[rowOffset + 1] << 8);
                            rowOffset += DetailBytesPerCell;
                        }
                    }

                    detailMaps.Add(detailMap);
                }

                return true;
            }

            #endregion
        }
    }
}
