using System;
using System.Security.Cryptography;
using System.Text;
using Game.Paths;

namespace Game.MapGeneration.Transfer
{
    /// <summary>
    ///     ワールド同一性IDの算出。generatedは生成入力（seed・生成マスタ指紋・生成器版）から導くので、同じ入力なら作り直しても同じIDになり共有キャッシュが命中する
    ///     templateは生成入力を持たないので作成時刻で区別する
    ///     World identity ids. A generated world derives from its generation inputs (seed, master fingerprint, generator version), so recreating it yields the same id and the shared cache hits
    ///     A template world owns no generation inputs, so its creation time tells worlds apart
    /// </summary>
    public static class WorldIdentity
    {
        public static string CalculateGenerated(int seed, string generationMasterFingerprint, string generatorVersion)
        {
            if (string.IsNullOrEmpty(generationMasterFingerprint))
                throw new InvalidOperationException("[WorldIdentity] A generated world id needs a non-empty generation master fingerprint.");
            return Hash($"{seed}:{generationMasterFingerprint}:{generatorVersion}");
        }

        public static string CalculateTemplate(int seed, string createdAt)
        {
            return Hash($"{seed}:{createdAt}");
        }

        private static string Hash(string source)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(source));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant().Substring(0, GameSystemPaths.WorldIdHexDigits);
        }
    }
}
