using System;
using System.IO;
using System.Security.Cryptography;
using Game.Paths;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.RemoteExec.Access
{
    // 起動ごとのトークンとWeb UIポートをユーザーのデータフォルダへ保存する
    // Store the per-boot token and Web UI port in the user's data directory
    public static class RemoteExecAccessFile
    {
        public const string HeaderName = "X-Remote-Exec-Token";
        public static string Token { get; private set; }
        public static string DirectoryPath => Path.Combine(GameSystemPaths.GameSystemDirectory, "RemoteExec");

        internal static void ClearToken()
        {
            Token = null;
        }

        public static void Issue(int port)
        {
            Token = null;
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var token = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

            // ディスクIO境界。書けたトークンだけを公開する
            // Publish only a persisted token at this disk IO boundary
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                var json = new JObject { ["port"] = port, ["token"] = token, ["processId"] = System.Diagnostics.Process.GetCurrentProcess().Id };
                var path = Path.Combine(DirectoryPath, "access.json");
                File.WriteAllText(path, json.ToString());
                Token = token;
                Debug.LogWarning($"[RemoteExec] 入口を開きました port:{port} access:{path}");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"[RemoteExec] access.json を書けませんでした（遠隔実行は使えません）: {e.Message}");
            }
        }
    }
}
