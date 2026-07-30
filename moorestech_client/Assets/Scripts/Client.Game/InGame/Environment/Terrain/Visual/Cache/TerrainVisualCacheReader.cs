using System.Collections.Generic;
using System.IO;
using System.Text;
using static Client.Game.InGame.Environment.Terrain.Visual.Cache.TerrainVisualCacheFormat;

namespace Client.Game.InGame.Environment.Terrain.Visual.Cache
{
    /// <summary>
    ///     書き出した見た目を読み戻す。全長が宣言された寸法と一致する場合だけ中身を信じる
    ///     Reads the written visuals back, trusting the content only when the total length matches the declared dimensions
    /// </summary>
    public static class TerrainVisualCacheReader
    {
        // キー不一致・未作成は素の取り逃し。キーが合うのに中身が寸法と食い違う場合だけbrokenReasonを返す
        // A missing file or a stale key is an ordinary miss; brokenReason is filled only when a matching key carries content that disagrees with its dimensions
        public static bool TryRead(
            string filePath, string expectedCacheKey, int expectedAlphamapResolution, int expectedLayerCount,
            int expectedDetailResolution, out TerrainTileVisual tileVisual, out string brokenReason)
        {
            tileVisual = null;
            brokenReason = null;

            if (!File.Exists(filePath)) return false;

            // キャッシュファイルは外部データ。ヘッダを信じる前に全長・魔法数・版・キーを確かめる
            // The cache file is external data: length, magic, version, and key are checked before the header is trusted
            var bytes = File.ReadAllBytes(filePath);
            if (bytes.Length < HeaderByteLength)
            {
                brokenReason = $"file length {bytes.Length} is shorter than the {HeaderByteLength}-byte header";
                return false;
            }

            if (ReadInt(bytes, 0) != MagicNumber || ReadInt(bytes, 4) != FormatVersion)
            {
                brokenReason = "magic number or format version is unsupported";
                return false;
            }
            if (Encoding.ASCII.GetString(bytes, 8, CacheKeyByteLength) != expectedCacheKey) return false;

            var alphamapResolution = ReadInt(bytes, AlphamapResolutionOffset);
            var layerCount = ReadInt(bytes, LayerCountOffset);
            var detailResolution = ReadInt(bytes, DetailResolutionOffset);
            var detailMapCount = ReadInt(bytes, DetailMapCountOffset);

            // 寸法は書き換えられ得る値。負や桁あふれを先に弾いてから長さ計算に使う
            // The dimensions are rewritable values, so negatives and overflow are rejected before any length is derived from them
            if (alphamapResolution <= 0 || layerCount <= 0 || detailResolution < 0 || detailMapCount < 0) return false;

            var alphamapByteLength = (long)alphamapResolution * alphamapResolution * layerCount;
            var detailByteLength = (long)detailMapCount * detailResolution * detailResolution * DetailBytesPerCell;
            if (bytes.Length != HeaderByteLength + alphamapByteLength + detailByteLength)
            {
                brokenReason = $"byte length {bytes.Length} disagrees with the declared dimensions";
                return false;
            }

            if (alphamapResolution != expectedAlphamapResolution)
            {
                brokenReason = $"alphamap resolution {alphamapResolution} disagrees with the expected {expectedAlphamapResolution}";
                return false;
            }

            // 層数はTerrainDataのterrainLayersと1対1。食い違ったままSetAlphamapsへ流すと全画素のテクスチャが入れ替わる
            // The layer count pairs one-to-one with TerrainData.terrainLayers; a mismatch reaching SetAlphamaps swaps every pixel's texture
            if (layerCount != expectedLayerCount)
            {
                brokenReason = $"layer count {layerCount} disagrees with the expected {expectedLayerCount}";
                return false;
            }

            if (0 < detailMapCount && detailResolution != expectedDetailResolution)
            {
                brokenReason = $"detail resolution {detailResolution} disagrees with the expected {expectedDetailResolution}";
                return false;
            }

            var readOffset = HeaderByteLength;
            tileVisual = new TerrainTileVisual(ReadAlphamap(), ReadDetailMaps());
            return true;

            #region Internal

            float[,,] ReadAlphamap()
            {
                var alphamap = new float[alphamapResolution, alphamapResolution, layerCount];
                for (var z = 0; z < alphamapResolution; z++)
                for (var x = 0; x < alphamapResolution; x++)
                for (var layer = 0; layer < layerCount; layer++)
                    alphamap[z, x, layer] = bytes[readOffset++] / WeightQuantizeScale;

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
                        detailMap[z, x] = bytes[readOffset] | (bytes[readOffset + 1] << 8);
                        readOffset += DetailBytesPerCell;
                    }

                    detailMaps.Add(detailMap);
                }

                return detailMaps;
            }

            #endregion
        }
    }
}
