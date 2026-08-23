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

        // 導出元は生成の入力だけ: 生成マスタ指紋（JSON原文＋PNG）・seed・2原点・解像度・生成器の版・配置台帳の指紋
        // 配置は同じ入力から決定論で出る建前だが、CPU世代差でpass-1が1件ずれた環境では検出できない。台帳そのものの指紋で塞ぐ
        // The inputs are generation's own: the master fingerprint (JSON text + PNGs), seed, the two origins, resolution, generator version and the placement ledger's digest
        // Placements are meant to derive deterministically from those, yet a CPU-generation difference shifting one pass-1 placement stays invisible; the ledger's own digest closes that
        public static string Compute(
            string generationMasterFingerprint, int seed, TerrainOrigins origins, int terrainResolution,
            string generatorVersion, string placementLedgerDigest)
        {
            if (string.IsNullOrEmpty(generationMasterFingerprint))
                throw new InvalidOperationException(
                    "[TerrainVisualCacheKey] The generation master fingerprint is empty: a generated world always owns one.");

            if (string.IsNullOrEmpty(generatorVersion))
                throw new InvalidOperationException(
                    "[TerrainVisualCacheKey] The generator version is empty: a generated world always declares one.");

            // 空の台帳でもSHA256は必ず64桁を返す。空文字は算出そのものを飛ばした合図なので黙って鍵にしない
            // An empty ledger still yields 64 hex characters, so a blank digest signals a skipped computation and never becomes a key silently
            if (string.IsNullOrEmpty(placementLedgerDigest))
                throw new InvalidOperationException(
                    "[TerrainVisualCacheKey] The placement ledger digest is empty: PlacementLedger.ComputeDigest always returns one.");

            var keySource = string.Join("|",
                generationMasterFingerprint,
                seed.ToString(CultureInfo.InvariantCulture),
                origins.NoiseOrigin.x.ToString(RoundTripFormat, CultureInfo.InvariantCulture),
                origins.NoiseOrigin.y.ToString(RoundTripFormat, CultureInfo.InvariantCulture),
                origins.SceneOrigin.x.ToString(RoundTripFormat, CultureInfo.InvariantCulture),
                origins.SceneOrigin.y.ToString(RoundTripFormat, CultureInfo.InvariantCulture),
                terrainResolution.ToString(CultureInfo.InvariantCulture),
                generatorVersion,
                placementLedgerDigest);

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
