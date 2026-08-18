using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Visual.Cache
{
    /// <summary>
    ///     見た目キャッシュの有効性を1本の文字列に畳み込む。splatmap/detailの導出元が1つでも動けば別のキーになる
    ///     Folds the visual cache's validity into one string; any change to a splatmap/detail input yields a different key
    /// </summary>
    public static class TerrainVisualCacheKey
    {
        // 浮動小数は"R"で往復可能な表記にする。桁を落とすと別の窓を同じキーとみなす
        // Floats use round-trippable "R"; truncating digits would treat a different window as the same key
        private const string RoundTripFormat = "R";

        // 導出元は5つ: マスタ原文・地形バイナリのハッシュ・ノイズ窓原点・seed・mapObject配置のダイジェスト
        // splatmapはSplatmapJobにworldOffsetとseedを直接渡すため、terrainHash(高さとバイオームのみ)では覆えない
        // Five inputs: the master text, the terrain binaries' hash, the noise window origin, the seed, and the map object digest
        // The splatmap feeds worldOffset and seed straight into SplatmapJob, which terrainHash (heights and biomes only) cannot cover
        // シーン原点は含めない。Terrainの設置座標にしか効かず、画素の中身を1つも変えないため
        // The scene origin is left out: it only moves where the Terrain stands and changes no pixel of the content
        public static string Compute(
            string generationMasterJsonText, string terrainHash, Vector2 noiseOrigin, int seed, byte[] mapObjectsDigest)
        {
            // 空ダイジェストは「mapObjectが0本」と区別できない。木の摂動と距離場が丸ごと抜けた見た目が焼き付く
            // An empty digest is indistinguishable from "zero map objects", baking in visuals that lost the tree perturbation and distance fields entirely
            if (mapObjectsDigest == null || mapObjectsDigest.Length == 0)
                throw new InvalidOperationException(
                    "[TerrainVisualCacheKey] The map objects digest is empty: MapObjectsDigest always folds even an empty layout into a hash.");

            if (string.IsNullOrEmpty(generationMasterJsonText))
                throw new InvalidOperationException(
                    "[TerrainVisualCacheKey] The generation master JSON text is empty: a generated world always owns one.");

            if (string.IsNullOrEmpty(terrainHash))
                throw new InvalidOperationException(
                    "[TerrainVisualCacheKey] The terrain hash is empty: a generated world always declares one.");

            // マスタ原文だけが可変長。先に固定長へ畳んでおかないと、区切りを跨いだ別の組み合わせが同じ連結文字列になる
            // The master text is the only variable-length input; folding it first stops a different split from forming the same joined string
            var keySource = string.Join("|",
                ToSha256Hex(generationMasterJsonText),
                terrainHash,
                seed.ToString(CultureInfo.InvariantCulture),
                noiseOrigin.x.ToString(RoundTripFormat, CultureInfo.InvariantCulture),
                noiseOrigin.y.ToString(RoundTripFormat, CultureInfo.InvariantCulture),
                ToHex(mapObjectsDigest));

            return ToSha256Hex(keySource);

            #region Internal

            string ToSha256Hex(string text)
            {
                using var sha256 = SHA256.Create();
                return ToHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(text)));
            }

            string ToHex(byte[] bytes)
            {
                return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }

            #endregion
        }
    }
}
