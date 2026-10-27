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
        // 公開待ちの接続情報の置き場。プロセスごとに分け、同時に起動した別プロセスと混ざらないようにする
        // Where the staged access details wait, one per process so concurrent boots never mix
        private static string StagedPath => Path.Combine(DirectoryPath, $"access-staged-{System.Diagnostics.Process.GetCurrentProcess().Id}.json");
        private static string _stagedToken;
        private static int _stagedPort;

        // 待受を開けたポートで接続情報を公開待ちに書く。外から見えない形で、Harmony を読む前に書けるかを確かめる
        // Stage the access details for the opened port, checking invisibly before Harmony loads that they can be written
        internal static bool Stage(int port)
        {
            Token = null;
            _stagedToken = null;

            // 正式な場所がディレクトリに塞がれていると公開で移せないので、ここで断る
            // A directory occupying the official place would block the publishing move, so refuse here
            var path = Path.Combine(DirectoryPath, FileName);
            if (Directory.Exists(path))
            {
                Debug.LogError($"[RemoteExec] access.json を書けませんでした（遠隔実行は使えません）: 置き場がディレクトリで塞がれています {path}");
                return false;
            }

            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var token = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            // ディスクIO境界。書けた接続情報だけを公開の候補にする
            // Disk IO boundary; only persisted access details become a candidate for publishing
            try
            {
                RemoteExecDirectory.EnsureCreated(DirectoryPath);
                var json = new JObject { ["port"] = port, ["token"] = token, ["processId"] = System.Diagnostics.Process.GetCurrentProcess().Id };
                File.WriteAllText(StagedPath, json.ToString());
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"[RemoteExec] access.json を書けませんでした（遠隔実行は使えません）: {e.Message}");
                return false;
            }
            _stagedToken = token;
            _stagedPort = port;
            Debug.Log($"[RemoteExec] 接続情報を公開待ちに書きました（Harmony を読み終えてから公開します） staged:{StagedPath}");
            return true;
        }

        // 公開待ちを正式な場所へ移して入口を開く。トークンを先に有効にし、読んですぐの要求を「トークン未発行」で拒まない
        // Open the entry by moving the staged file into place; the token goes live first so a request right after reading is not refused
        internal static bool Publish()
        {
            var path = Path.Combine(DirectoryPath, FileName);
            Token = _stagedToken;
            _stagedToken = null;
            // ディスクIO境界。移せなければトークンも戻し、入口の撤去は呼び出し側の Deactivate に任せる
            // Disk IO boundary; when the move fails the token is withdrawn too, and the caller's Deactivate removes the entry
            try
            {
                if (File.Exists(path)) File.Delete(path);
                File.Move(StagedPath, path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Token = null;
                Debug.LogError($"[RemoteExec] access.json を公開できませんでした（遠隔実行は使えません）: {e.Message}");
                return false;
            }
            Debug.LogWarning($"[RemoteExec] 入口を開きました port:{_stagedPort} access:{path}");
            return true;
        }

        // 無効な起動と正常終了で撤去する。残すと死んだプロセスのトークンが有効な入口として読まれる
        // Withdraw it on a disabled boot and on a clean exit; leaving it lets a dead process's token read as a live entry
        internal static void Remove()
        {
            Token = null;
            _stagedToken = null;
            RemoveStaged();
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

        // 公開待ちは自プロセスだけの物なので、所有者を確かめずに消す。ディスクIO境界で、消せなければ理由を残す
        // The staged file belongs to this process alone, so it is deleted unchecked; at this disk IO boundary a failure leaves its reason
        private static void RemoveStaged()
        {
            try
            {
                if (File.Exists(StagedPath)) File.Delete(StagedPath);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"[RemoteExec] 公開待ちの接続情報を撤去できませんでした（トークンが残ります） {StagedPath}: {e.Message}");
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
