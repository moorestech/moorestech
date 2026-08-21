using System.Security.Cryptography;
using System.Text;

namespace Game.MapGeneration.Identity
{
    // seedとcreatedAtからワールド同一性IDを作る。転送メタと共有キャッシュの両方がこの1式を使う
    // Builds the world identity id from seed and createdAt; both the transfer meta and the shared cache use this one formula
    public static class WorldIdentity
    {
        private const int WorldIdHexDigits = 16;

        public static string Calculate(int seed, string createdAt)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{seed}:{createdAt}"));
            return System.BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant().Substring(0, WorldIdHexDigits);
        }
    }
}
