using System;
using System.IO;
using System.Security.Cryptography;

namespace Game.Paths
{
    public static class CalcFileHash
    {
        public static string GetSha1Hash(string filePath)
        {
            using var sha1 = new SHA1Managed();
            using var stream = File.OpenRead(filePath);
            var hash = sha1.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }
    }
}