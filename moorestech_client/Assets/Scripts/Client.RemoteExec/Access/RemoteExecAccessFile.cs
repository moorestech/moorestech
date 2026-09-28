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
            // 置き場はプロセス非依存の1ファイル。他プロセスの生きた入口を消さないよう所有者を確かめる
            // The location is one process-independent file, so ownership is checked before deleting another process's live entry
            if (!IsOwnEntryOrDead(path, out var skipReason))
            {
                Debug.LogWarning($"[RemoteExec] 入口の撤去を見送りました（{skipReason}） access:{path}");
                return;
            }
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

        // 自分の入口か、書き手が既に死んでいる残骸だけを撤去対象とする。判定できない入口は触らない
        // Only this process's own entry, or the leftovers of a writer already gone, may be withdrawn; an undecidable entry is left alone
        private static bool IsOwnEntryOrDead(string path, out string skipReason)
        {
            skipReason = null;
            int recordedProcessId;
            // 他プロセスが書いた外部JSONの読み取り。読めない入口は所有者不明として残す
            // Reading external JSON written by another process; an unreadable entry stays as an unknown owner
            try
            {
                if (!File.Exists(path)) return true;
                var token = JObject.Parse(File.ReadAllText(path))["processId"];
                if (token == null || token.Type != JTokenType.Integer)
                {
                    skipReason = "processId が無い/整数でないため誰の入口か判定できない";
                    return false;
                }
                recordedProcessId = (int)token;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is Newtonsoft.Json.JsonException)
            {
                skipReason = $"access.json を読めないため誰の入口か判定できない: {e.Message}";
                return false;
            }

            if (recordedProcessId == System.Diagnostics.Process.GetCurrentProcess().Id) return true;

            // 生存確認はOS境界。生きていれば触らず、居なければ残骸として撤去する
            // Liveness probing is an OS boundary: a live writer is left alone and a missing one is withdrawn as leftovers
            try
            {
                System.Diagnostics.Process.GetProcessById(recordedProcessId);
                skipReason = $"生きている別プロセスの入口 pid:{recordedProcessId}";
                return false;
            }
            catch (ArgumentException)
            {
                return true;
            }
            catch (InvalidOperationException e)
            {
                skipReason = $"pid:{recordedProcessId} の生存を確認できない: {e.Message}";
                return false;
            }
        }
    }
}
