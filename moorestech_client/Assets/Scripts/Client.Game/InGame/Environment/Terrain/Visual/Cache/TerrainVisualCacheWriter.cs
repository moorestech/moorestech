using System;
using System.IO;
using System.Text;
using UnityEngine;
using static Client.Game.InGame.Environment.Terrain.Visual.Cache.TerrainVisualCacheFormat;

namespace Client.Game.InGame.Environment.Terrain.Visual.Cache
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

            var bytes = new byte[HeaderByteLength
                                 + alphamapResolution * alphamapResolution * layerCount
                                 + detailMapCount * detailResolution * detailResolution * DetailBytesPerCell];

            WriteHeader();
            var writeOffset = HeaderByteLength;
            WriteAlphamap();
            WriteDetailMaps();

            WriteAtomically();

            #region Internal

            void WriteHeader()
            {
                WriteInt(bytes, 0, MagicNumber);
                WriteInt(bytes, 4, FormatVersion);
                Encoding.ASCII.GetBytes(cacheKey, 0, CacheKeyByteLength, bytes, 8);
                WriteInt(bytes, AlphamapResolutionOffset, alphamapResolution);
                WriteInt(bytes, LayerCountOffset, layerCount);
                WriteInt(bytes, DetailResolutionOffset, detailResolution);
                WriteInt(bytes, DetailMapCountOffset, detailMapCount);
            }

            void WriteAlphamap()
            {
                for (var z = 0; z < alphamapResolution; z++)
                for (var x = 0; x < alphamapResolution; x++)
                for (var layer = 0; layer < layerCount; layer++)
                    bytes[writeOffset++] = (byte)Mathf.Clamp(
                        Mathf.RoundToInt(tileVisual.Alphamap[z, x, layer] * WeightQuantizeScale), 0, byte.MaxValue);
            }

            void WriteDetailMaps()
            {
                foreach (var detailMap in tileVisual.DetailMaps)
                for (var z = 0; z < detailResolution; z++)
                for (var x = 0; x < detailResolution; x++)
                {
                    // 密度は1セルあたりの本数。ushortに収まらない値はこの形式では表せないので黙って切らずに落とす
                    // Density is the instance count per cell; a value beyond ushort is unrepresentable here, so it fails instead of being clipped
                    var density = detailMap[z, x];
                    if (density < 0 || ushort.MaxValue < density)
                        throw new InvalidOperationException(
                            $"[TerrainVisualCacheWriter] Detail density {density} does not fit the 16-bit cache format.");

                    bytes[writeOffset++] = (byte)(density & 0xFF);
                    bytes[writeOffset++] = (byte)((density >> 8) & 0xFF);
                }
            }

            // 書き込み途中で落ちた半端なファイルを本体名で残さない。完成品だけを一手で置き換える
            // A half-written file never lands under the real name; only the finished bytes replace it in one move
            void WriteAtomically()
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                var temporaryFilePath = filePath + ".writing";
                File.WriteAllBytes(temporaryFilePath, bytes);
                if (File.Exists(filePath)) File.Delete(filePath);
                File.Move(temporaryFilePath, filePath);
            }

            #endregion
        }
    }
}
