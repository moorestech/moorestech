using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Game.MapGeneration.Transfer;

namespace Game.MapGeneration.Cache
{
    /// <summary>
    ///     見た目キャッシュの有効性を1本の文字列に畳み込む。生成の入力が1つでも動けば別のキーになる
    ///     Folds the visual cache's validity into one string; any change to generation's own inputs yields a different key
    /// </summary>
    public static class TerrainVisualCacheKey
    {
        // 浮動小数は"R"で往復可能な表記にする。桁を落とすと別の窓を同じキーとみなす
        // Floats use round-trippable "R"; truncating digits would treat a different window as the same key
        private const string RoundTripFormat = "R";

        // 導出元は生成の入力だけ: 生成マスタ指紋（JSON原文＋PNG）・seed・2原点・解像度・生成器の版。配置は同じ入力から決定論で出るので鍵に入れない
        // The inputs are generation's own: the master fingerprint (JSON text + PNGs), seed, the two origins, resolution and generator version; placements derive deterministically from them and stay out of the key
        public static string Compute(string generationMasterFingerprint, int seed, TerrainOrigins origins, int terrainResolution, string generatorVersion)
        {
            if (string.IsNullOrEmpty(generationMasterFingerprint))
                throw new InvalidOperationException(
                    "[TerrainVisualCacheKey] The generation master fingerprint is empty: a generated world always owns one.");

            if (string.IsNullOrEmpty(generatorVersion))
                throw new InvalidOperationException(
                    "[TerrainVisualCacheKey] The generator version is empty: a generated world always declares one.");

            var keySource = string.Join("|",
                generationMasterFingerprint,
                seed.ToString(CultureInfo.InvariantCulture),
                origins.NoiseOrigin.x.ToString(RoundTripFormat, CultureInfo.InvariantCulture),
                origins.NoiseOrigin.y.ToString(RoundTripFormat, CultureInfo.InvariantCulture),
                origins.SceneOrigin.x.ToString(RoundTripFormat, CultureInfo.InvariantCulture),
                origins.SceneOrigin.y.ToString(RoundTripFormat, CultureInfo.InvariantCulture),
                terrainResolution.ToString(CultureInfo.InvariantCulture),
                generatorVersion);

            return ToSha256Hex(keySource);

            #region Internal

            string ToSha256Hex(string text)
            {
                using var sha256 = SHA256.Create();
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
                return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }

            #endregion
        }
    }
}
