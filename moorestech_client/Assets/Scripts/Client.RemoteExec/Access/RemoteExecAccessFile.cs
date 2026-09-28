using System;
using System.IO;
using System.Security.Cryptography;
using Game.Paths;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.RemoteExec.Access
{
    // 起動毎のトークン/ポートを保存
    // Persist per-boot token/port
    public static class RemoteExecAccessFile
    {
        internal const string HeaderName = "X-Remote-Exec-Token";
        internal const string FileName = "access.json";
        internal static string Token { get; private set; }
        public static string DirectoryPath => GameSystemPaths.RemoteExecDirectory;

        internal static void ClearToken()
        {
            Token = null;
        }

        // ポート未確定では発行しない。0は実ポート域外で、書けば届かない入口を名乗る
        // Never issue before the port is known; 0 is outside the real port range and would advertise an entry nothing can reach
        internal static void Issue(int? port)
        {
            Token = null;
            if (port == null)
            {
                Debug.LogError("[RemoteExec] Web UI の実ポートが未確定のため入口を開けません（遠隔実行は使えません）");
                return;
            }
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var token = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

            // ディスクIO境界。書けたトークンだけを公開する
            // Publish only a persisted token at this disk IO boundary
            try
            {
                RemoteExecDirectory.EnsureCreated(DirectoryPath);
                var json = new JObject { ["port"] = port.Value, ["token"] = token, ["processId"] = System.Diagnostics.Process.GetCurrentProcess().Id };
                var path = Path.Combine(DirectoryPath, FileName);
                File.WriteAllText(path, json.ToString());
                Token = token;
                Debug.LogWarning($"[RemoteExec] 入口を開きました port:{port.Value} access:{path}");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"[RemoteExec] access.json を書けませんでした（遠隔実行は使えません）: {e.Message}");
            }
        }

        // 無効な起動と正常終了で撤去する。残すと死んだプロセスのトークンが有効な入口として読まれる
        // Withdraw it on a disabled boot and on a clean exit; leaving it lets a dead process's token read as a live entry
        internal static void Remove()
        {
            Token = null;
            var path = Path.Combine(DirectoryPath, FileName);
            // ディスクIO境界。撤去できなかった事実を必ず残す
            // Disk IO boundary; a failed withdrawal always leaves its reason
            try
            {
                if (!File.Exists(path)) return;
                File.Delete(path);
                Debug.Log($"[RemoteExec] 入口を撤去しました access:{path}");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"[RemoteExec] access.json を撤去できませんでした（死んだプロセスのトークンが残ります） {path}: {e.Message}");
            }
        }
    }
}
