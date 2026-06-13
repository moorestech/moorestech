using System;
using System.Security.Cryptography;
using System.Text;

namespace Core.Master
{
    /// <summary>
    ///     seed文字列からMD5ベースで決定的にGUIDを生成するユーティリティ（RFC 4122 UUIDv5ではない。MD5の16バイトをそのまま使用）
    ///     Utility generating an MD5-based deterministic GUID from a seed string (not RFC 4122 UUIDv5; uses the 16 MD5 bytes directly)
    /// </summary>
    public static class DeterministicGuidUtil
    {
        public static Guid Create(string seed)
        {
            // 暗号用途ではないためMD5で十分（要件は衝突耐性ではなく決定性）
            // MD5 is sufficient since this is not cryptographic (the requirement is determinism, not collision resistance)
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(seed));
            return new Guid(hash);
        }
    }
}
