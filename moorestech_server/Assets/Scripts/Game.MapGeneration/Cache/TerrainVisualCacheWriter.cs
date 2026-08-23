using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using static Game.MapGeneration.Cache.TerrainVisualCacheFormat;

namespace Game.MapGeneration.Cache
{
    /// <summary>
    ///     タイル1枚ぶんの見た目を生バイナリで書き出す。先頭にキーと寸法を置き、読み手が全長で検算できる形にする
    ///     Writes one tile's visuals as raw binary, leading with the key and dimensions so the reader can verify by total length
    /// </summary>
    public static class TerrainVisualCacheWriter
    {
        public static void Write(string filePath, string cacheKey, TerrainTileVisual tileVisual)
        {
            if (cacheKey.Length != CacheKeyByteLength)
                throw new InvalidOperationException(
                    $"[TerrainVisualCacheWriter] The cache key must be {CacheKeyByteLength} hex characters but was {cacheKey.Length}.");

            var heightmapResolution = tileVisual.DisplayHeights.GetLength(0);
            var alphamapResolution = tileVisual.AlphamapResolution;
            var layerCount = tileVisual.AlphamapLayerCount;
            var detailMapCount = tileVisual.DetailMaps.Count;
            var detailResolution = detailMapCount == 0 ? 0 : tileVisual.DetailMaps[0].GetLength(0);

            var headerBytes = new byte[HeaderByteLength];
            WriteHeader();

            // 平面はそのまま書けるので複製しない。高さとdetailだけ行バッファへ詰めて逐次SHAを回す
            // The planes are written as they are with no copy; only heights and detail pass through a row buffer under the incremental SHA
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            var temporaryFilePath = filePath + ".writing";
            using (var stream = new FileStream(temporaryFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var payloadHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                stream.Write(headerBytes, 0, headerBytes.Length);
                WriteHeights(stream, payloadHash);
                WriteAlphamapPlanes(stream, payloadHash);
                WriteDetailMaps(stream, payloadHash);

                var checksum = payloadHash.GetHashAndReset();
                stream.Position = PayloadChecksumOffset;
                stream.Write(checksum, 0, PayloadChecksumByteLength);
                stream.Flush();
            }

            if (File.Exists(filePath)) File.Delete(filePath);
            File.Move(temporaryFilePath, filePath);

            #region Internal

            void WriteHeader()
            {
                WriteInt(headerBytes, 0, MagicNumber);
                WriteInt(headerBytes, 4, FormatVersion);
                Encoding.ASCII.GetBytes(cacheKey, 0, CacheKeyByteLength, headerBytes, 8);
                WriteInt(headerBytes, HeightmapResolutionOffset, heightmapResolution);
                WriteInt(headerBytes, AlphamapResolutionOffset, alphamapResolution);
                WriteInt(headerBytes, LayerCountOffset, layerCount);
                WriteInt(headerBytes, DetailResolutionOffset, detailResolution);
                WriteInt(headerBytes, DetailMapCountOffset, detailMapCount);
            }

            void WriteHeights(FileStream stream, IncrementalHash payloadHash)
            {
                var rowBytes = new byte[heightmapResolution * HeightBytesPerPixel];
                for (var z = 0; z < heightmapResolution; z++)
                {
                    var rowOffset = 0;
                    for (var x = 0; x < heightmapResolution; x++)
                    {
                        var quantizedHeight = (ushort)Mathf.Clamp(
                            Mathf.RoundToInt(tileVisual.DisplayHeights[z, x] * HeightQuantizeScale), 0, ushort.MaxValue);
                        rowBytes[rowOffset++] = (byte)(quantizedHeight & 0xFF);
                        rowBytes[rowOffset++] = (byte)((quantizedHeight >> 8) & 0xFF);
                    }
                    stream.Write(rowBytes, 0, rowBytes.Length);
                    payloadHash.AppendData(rowBytes);
                }
            }

            void WriteAlphamapPlanes(FileStream stream, IncrementalHash payloadHash)
            {
                var expectedPlaneByteLength = (int)AlphamapPlaneByteLength(alphamapResolution);
                if (tileVisual.AlphamapPlanes.Count != AlphamapPlaneCount(layerCount))
                    throw new InvalidOperationException(
                        $"[TerrainVisualCacheWriter] {layerCount} layers need {AlphamapPlaneCount(layerCount)} planes " +
                        $"but {tileVisual.AlphamapPlanes.Count} were given.");

                foreach (var plane in tileVisual.AlphamapPlanes)
                {
                    if (plane.Length != expectedPlaneByteLength)
                        throw new InvalidOperationException(
                            $"[TerrainVisualCacheWriter] An alphamap plane holds {plane.Length} bytes but resolution " +
                            $"{alphamapResolution} requires {expectedPlaneByteLength}.");

                    stream.Write(plane, 0, plane.Length);
                    payloadHash.AppendData(plane);
                }
            }

            void WriteDetailMaps(FileStream stream, IncrementalHash payloadHash)
            {
                var rowBytes = new byte[detailResolution * DetailBytesPerCell];
                foreach (var detailMap in tileVisual.DetailMaps)
                for (var z = 0; z < detailResolution; z++)
                {
                    var rowOffset = 0;
                    for (var x = 0; x < detailResolution; x++)
                    {
                        // 密度は1セルあたりの本数。ushortに収まらない値はこの形式では表せないので黙って切らずに落とす
                        // Density is the instance count per cell; a value beyond ushort is unrepresentable here, so it fails instead of being clipped
                        var density = detailMap[z, x];
                        if (density < 0 || ushort.MaxValue < density)
                            throw new InvalidOperationException(
                                $"[TerrainVisualCacheWriter] Detail density {density} does not fit the 16-bit cache format.");

                        rowBytes[rowOffset++] = (byte)(density & 0xFF);
                        rowBytes[rowOffset++] = (byte)((density >> 8) & 0xFF);
                    }
                    stream.Write(rowBytes, 0, rowBytes.Length);
                    payloadHash.AppendData(rowBytes);
                }
            }

            #endregion
        }
    }
}
