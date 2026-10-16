using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Client.Game.InGame.BugReport.BuildOrigin;
using Game.Paths;
using UnityEngine;
using Debug = UnityEngine.Debug;
namespace Client.Game.InGame.BugReport
{
    // ・Editorはgitで状態取得
    // ・ビルドは焼込情報を参照
    // - Probe Editor working trees via git
    // - Read baked build-info.json in builds
    public static class RepositoryStateProbe
    {
        public const string BuildInfoFileName = "build-info.json";
        public static string RepositoryRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        // マスタrepoの置き場はピンの relativePath だけが定義元。隣の名前を推測すると別repoの状態をマスタとして名乗る
        // The pin's relativePath is the sole definition of where the master repo sits; guessing the neighbour's name would report another repo as the master
        public static string MasterDataRoot => MasterDataRootLocator.ResolveForPrimaryRepository();
        // 作業ツリーは書き換えない。bundle作成時だけ refs/bugreport/* の一時ref操作を許す
        // Never modifies the working tree; temporary refs/bugreport/* mutations are allowed only for bundle creation
        public static RepositoryProbeResult ProbeGit(string repositoryRoot)
        {
            var result = new RepositoryProbeResult();
            if (!Directory.Exists(repositoryRoot))
            {
                Debug.LogWarning($"リポジトリ状態を取れません（ディレクトリが無い）: {repositoryRoot}");
                result.Error = $"ディレクトリが無い: {repositoryRoot}";
                return result;
            }
            if (!TryGit(repositoryRoot, "rev-parse HEAD", out var commit, out var error))
            {
                Debug.LogWarning($"リポジトリ状態を取れません: {error}");
                result.Error = error;
                return result;
            }
            var branchRead = Query(repositoryRoot, "rev-parse --abbrev-ref HEAD", result, out var branch);
            var statusRead = Query(repositoryRoot, "status --porcelain", result, out var status);
            // バイナリ差分まで持ち帰らないと、画像やアセットの未コミット変更が欠けたまま別の状態で再現される
            // Without binary diffs the uncommitted changes to images and assets are lost and reproduction runs a different state
            Query(repositoryRoot, "diff --binary --full-index HEAD", result, out var diff);
            Query(repositoryRoot, "ls-files --others --exclude-standard", result, out var untracked);
            // status が取れないときに Dirty=false と名乗ると「差分なしのクリーンな作業ツリー」として再現されてしまう
            // Claiming Dirty=false when status is unreadable would reproduce the report as a clean working tree with no edits
            var dirty = !statusRead || status.Trim().Length > 0;
            result.State = new RepositoryState { Commit = commit.Trim(), Branch = branchRead ? branch.Trim() : "", Dirty = dirty };
            result.TrackedChangesPresent = !statusRead || HasTrackedChange();
            result.DiffText = diff;
            result.UntrackedFiles = new List<string>(untracked.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            return result;
            #region Internal
            // 未追跡だけのdirtyでは diff HEAD は空が正常。先頭2文字が ?? 以外の行だけを追跡ファイルの変更とみなす
            // With untracked-only dirt an empty diff HEAD is normal; only lines whose first two characters are not ?? count as tracked changes
            bool HasTrackedChange()
            {
                foreach (var line in status.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!line.StartsWith("??", StringComparison.Ordinal)) return true;
                }
                return false;
            }
            #endregion
        }
        // 失敗した問い合わせは結果へ理由を積む。捨てると「取れなかった」と「空だった」が同じ見た目になる
        // A failed query pushes its reason into the result; discarding it makes "unreadable" and "empty" look identical
        private static bool Query(string repositoryRoot, string arguments, RepositoryProbeResult result, out string stdout)
        {
            if (TryGit(repositoryRoot, arguments, out stdout, out var error)) return true;
            Debug.LogWarning($"リポジトリ状態の一部を取れません: {error}");
            result.QueryFailures.Add(error);
            return false;
        }
        // 出所を読む唯一の実装（ADR 0059・F01）。Editor・焼き込み情報つきビルド・焼き込み情報の無いビルドの3状態で返す
        // The sole implementation reading the origin (ADR 0059, F01), returning one of three states: Editor, baked build, build without info
        // パースと既存消費側への射影は BuildInfoJson へ委譲する（本ファイルの行数分割）
        // Parsing and projection for existing consumers live in BuildInfoJson (kept out of this file to stay under the line limit)
        public static BuildOriginReading ReadBuildOrigin()
        {
            // build-info.json はビルドにしか焼かれない。Editorで読まない判断は唯一の読み手であるここが持つ（呼び出し側へ複製しない）
            // build-info.json is baked only into builds; the "never read it in the Editor" rule lives in this single reader, never copied to callers
            if (Application.isEditor)
            {
                Debug.Log("Editor実行のため build-info.json は読まず buildInfo は null になります（作業ツリーの状態はgit probeが名乗る）");
                return BuildOriginReading.Editor();
            }
            var path = GameSystemPaths.BuildInfoFilePath;
            if (!File.Exists(path))
            {
                var absentReason = $"配布ビルドに build-info.json が無いため出所が不明 path:{path}";
                Debug.LogWarning(absentReason);
                return BuildOriginReading.WithoutInfo(absentReason);
            }
            // ディスク読みは外部境界（ロック・権限・容量）。読めない出所は起動を落とさず「出所不明」へ畳む
            // The disk read is an external boundary (locks, permissions, capacity); an unreadable origin folds into "unknown" instead of failing the boot
            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                var unreadableReason = $"配布ビルドの build-info.json を読めないため出所が不明 path:{path} message:{e.Message}";
                Debug.LogError(unreadableReason);
                return BuildOriginReading.WithoutInfo(unreadableReason);
            }
            var buildInfo = BuildInfoJson.Parse(json);
            if (buildInfo == null) return BuildOriginReading.WithoutInfo($"配布ビルドの build-info.json を解釈できないため出所が不明 path:{path}");
            return BuildOriginReading.Baked(buildInfo);
        }
        // 焼き込み情報だけが欲しい消費側（進行記録）への射影。Editorと焼き込み情報の無いビルドではnull
        // A projection for consumers that want only the baked info (progress records); null for the Editor and for a build without info
        public static BuildInfo ReadBuildInfo() => ReadBuildOrigin().BuildInfo;
        internal static bool TryGit(string workingDirectory, string arguments, out string stdout, out string error)
            => TryGit(workingDirectory, arguments, false, out stdout, out error);
        internal static bool TryGit(string workingDirectory, string arguments, bool remoteQuery, out string stdout, out string error)
        {
            const int remoteTimeoutMilliseconds = 15000;
            // quotepathを切らないと非ASCIIのパスが\xxx形式へ化け、未追跡ファイルのコピー元を見失う
            // Without disabling quotepath, non-ASCII paths come back escaped and the untracked copy loses its source
            // 所有者が実行ユーザーと違うだけでgitはdubious ownershipで全問い合わせを拒む。読み取り専用の問い合わせに所有者判定は要らない
            // Differing directory ownership alone makes git refuse every query as dubious; these read-only queries need no ownership check
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-c core.quotepath=false -c \"safe.directory={workingDirectory}\" " + arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            // 公開確認は認証UIを出さず、失敗時にローカル資料へ切り替える
            // Publication checks never prompt; failures fall back to local evidence
            if (remoteQuery)
            {
                startInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
                startInfo.EnvironmentVariables["GIT_SSH_COMMAND"] = "ssh -oBatchMode=yes -oStrictHostKeyChecking=yes";
                startInfo.EnvironmentVariables["GIT_SSH_VARIANT"] = "ssh";
                startInfo.Arguments = "-c credential.helper= -c core.askPass= " + startInfo.Arguments;
            }
            // 外部プロセス起動は例外を返す境界のため、ここに限りcatchして失敗理由へ変換する
            // Process spawning is an external boundary; only here we catch and convert failures into a reason
            try
            {
                using var process = Process.Start(startInfo);
                // stderrを先に非同期で排水する。stdoutだけ読むと64KB超のstderrで子がwriteブロックしハングする
                // Drain stderr asynchronously first; reading only stdout hangs the child once stderr exceeds the 64KB pipe
                var timer = Stopwatch.StartNew();
                var stderrTask = process.StandardError.ReadToEndAsync();
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var reads = System.Threading.Tasks.Task.WhenAll(stdoutTask, stderrTask);
                // 外部プロセス境界でpipe終端とgit終了を同じ期限内で待ち、自分のtransportも停止する
                // At the external process boundary, bound pipe EOF and git exit and stop our transport on expiry
                if (remoteQuery && (!reads.Wait(remoteTimeoutMilliseconds) ||
                    !process.WaitForExit((int)Math.Max(0, remoteTimeoutMilliseconds - timer.ElapsedMilliseconds))))
                {
                    stdout = "";
                    error = $"git {arguments} timed out after {remoteTimeoutMilliseconds}ms";
                    var windows = System.Environment.OSVersion.Platform == PlatformID.Win32NT;
                    try
                    {
                        using var killer = Process.Start(new ProcessStartInfo
                        {
                            FileName = windows ? "taskkill.exe" : "/bin/sh",
                            // 親を停止してから子孫を集め、末端から終了して全PIDの消滅を期限内で確認する
                            // Freeze parents before collecting descendants, kill leaves first, then bound verification of all PIDs
                            Arguments = windows ? $"/F /T /PID {process.Id}" : "-c \"" +
                                "collect() { kill -STOP $1 || return; for child in $(/usr/bin/pgrep -P $1); do collect $child; done; pids=\\\"$pids $1\\\"; }; " +
                                $"collect {process.Id}; for pid in $pids; do kill -KILL $pid; done; " +
                                $"for attempt in 1 2 3 4 5 6 7 8 9 10; do alive=; for pid in $pids; do [ $pid = {process.Id} ] && continue; kill -0 $pid 2>/dev/null && alive=1; done; " +
                                "[ -z $alive ] && exit 0; sleep 0.1; done; echo 'git descendants still present' >&2; exit 2\"",
                            UseShellExecute = false, CreateNoWindow = true,
                        });
                        if (!killer.WaitForExit(1500)) { killer.Kill(); Debug.LogError("git transport cleanup timed out"); }
                        else if (killer.ExitCode != 0 && killer.ExitCode != 1) Debug.LogError($"git transport cleanup failed: {killer.ExitCode}");
                    }
                    finally { if (!process.HasExited) process.Kill(); }
                    if (!process.WaitForExit(1500)) Debug.LogError($"git cleanup incomplete: PID {process.Id}");
                    Debug.LogWarning(error);
                    return false;
                }
                stdout = stdoutTask.GetAwaiter().GetResult();
                process.WaitForExit();
                error = process.ExitCode == 0 ? null : $"git {arguments} failed ({process.ExitCode}): {stderrTask.Result.Trim()}";
                return process.ExitCode == 0;
            }
            catch (Exception exception)
            {
                stdout = "";
                error = $"git を起動できない: {exception.GetBaseException().Message}";
                return false;
            }
        }
    }
}
