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

            var alphamapResolution = tileVisual.Alphamap.GetLength(0);
            var layerCount = tileVisual.Alphamap.GetLength(2);
            var detailMapCount = tileVisual.DetailMaps.Count;
            var detailResolution = detailMapCount == 0 ? 0 : tileVisual.DetailMaps[0].GetLength(0);

            var headerBytes = new byte[HeaderByteLength];
            WriteHeader();

            // payload全体を複製せず、行バッファと逐次SHAだけで一時ファイルへ書き出す
            // Stream through row buffers and incremental SHA into the temporary file without duplicating the whole payload
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            var temporaryFilePath = filePath + ".writing";
            using (var stream = new FileStream(temporaryFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var payloadHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                stream.Write(headerBytes, 0, headerBytes.Length);
                WriteAlphamap(stream, payloadHash);
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
                WriteInt(headerBytes, AlphamapResolutionOffset, alphamapResolution);
                WriteInt(headerBytes, LayerCountOffset, layerCount);
                WriteInt(headerBytes, DetailResolutionOffset, detailResolution);
                WriteInt(headerBytes, DetailMapCountOffset, detailMapCount);
            }

            void WriteAlphamap(FileStream stream, IncrementalHash payloadHash)
            {
                var rowBytes = new byte[alphamapResolution * layerCount];
                for (var z = 0; z < alphamapResolution; z++)
                {
                    var rowOffset = 0;
                    for (var x = 0; x < alphamapResolution; x++)
                    for (var layer = 0; layer < layerCount; layer++)
                        rowBytes[rowOffset++] = (byte)Mathf.Clamp(
                            Mathf.RoundToInt(tileVisual.Alphamap[z, x, layer] * WeightQuantizeScale), 0, byte.MaxValue);
                    stream.Write(rowBytes, 0, rowBytes.Length);
                    payloadHash.AppendData(rowBytes);
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
