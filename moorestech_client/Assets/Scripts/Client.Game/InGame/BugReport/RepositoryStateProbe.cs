using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Client.Game.InGame.BugReport
{
    public sealed class RepositoryProbeResult
    {
        public RepositoryState State;
        public string DiffText = "";
        public List<string> UntrackedFiles = new();

        // HEADすら取れず状態を1つも名乗れないときの理由。これが入った結果は State が null
        // Why nothing at all could be read (not even HEAD); such a result carries a null State
        public string Error;

        // 取れた問い合わせは使いつつ、取れなかった問い合わせの理由を1つずつ残す
        // Keeps whatever could be read while recording, one by one, why the rest could not
        public List<string> QueryFailures = new();

        // 追跡ファイルに変更がある状態。未追跡だけのdirtyと区別しないと「差分が空」の異常を検出できない
        // Tracked files have changes; without separating this from untracked-only dirt, an empty diff cannot be flagged
        public bool TrackedChangesPresent;
    }

    // Editor実行時は git で作業ツリーの状態を取り、ビルド実行時は焼き込まれた build-info.json を読む
    // Probes the working tree via git when running in the Editor; reads the baked build-info.json in a build
    public static class RepositoryStateProbe
    {
        public const string BuildInfoFileName = "build-info.json";
        public static string RepositoryRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        public static string MasterDataRoot => Path.GetFullPath(Path.Combine(RepositoryRoot, "..", "moorestech_master"));

        // 読み取り専用の問い合わせだけを行う。作業ツリーを書き換えるgitコマンドはここに足してはならない
        // Runs read-only queries only; a git command that mutates the working tree must never be added here
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
            Query(repositoryRoot, "diff HEAD", result, out var diff);
            Query(repositoryRoot, "ls-files --others --exclude-standard", result, out var untracked);

            // status が取れないときに Dirty=false と名乗ると「差分なしのクリーンな作業ツリー」として再現されてしまう
            // Claiming Dirty=false when status is unreadable would reproduce the report as a clean working tree with no edits
            var dirty = !statusRead || status.Trim().Length > 0;
            result.State = new RepositoryState { Commit = commit.Trim(), Branch = branchRead ? branch.Trim() : "", Dirty = dirty };
            result.TrackedChangesPresent = !statusRead || HasTrackedChange(status);
            result.DiffText = diff;
            result.UntrackedFiles = new List<string>(untracked.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            return result;
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

        // 未追跡だけのdirtyでは diff HEAD は空が正常。先頭2文字が ?? 以外の行だけを追跡ファイルの変更とみなす
        // With untracked-only dirt an empty diff HEAD is normal; only lines whose first two characters are not ?? count as tracked changes
        private static bool HasTrackedChange(string statusPorcelain)
        {
            foreach (var line in statusPorcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!line.StartsWith("??", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        public static RepositoryState ReadBuildInfo()
        {
            var path = Path.Combine(Application.streamingAssetsPath, BuildInfoFileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"build-info.json が無いためリポジトリ状態は不明です path:{path}");
                return new RepositoryState { Commit = "", Branch = "", Dirty = false };
            }

            var json = JObject.Parse(File.ReadAllText(path));
            return new RepositoryState { Commit = (string)json["commit"], Branch = (string)json["branch"], Dirty = (bool)json["dirty"] };
        }

        // ビルド時に焼き込む内容を組み立てる。Editorアセンブリを参照できないテストからも検証できるようここに置く
        // Composes what a build bakes in; it lives here so tests that cannot reference the Editor assembly can verify it
        public static string ComposeBuildInfoJson(RepositoryProbeResult repo, RepositoryProbeResult master, DateTime builtAt)
        {
            var info = new JObject
            {
                ["commit"] = repo.State?.Commit ?? "",
                ["branch"] = repo.State?.Branch ?? "",
                ["dirty"] = repo.State?.Dirty ?? false,
                ["masterCommit"] = master.State?.Commit ?? "",
                ["masterDirty"] = master.State?.Dirty ?? false,
                ["builtAt"] = builtAt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            };
            return info.ToString(Formatting.Indented);
        }

        private static bool TryGit(string workingDirectory, string arguments, out string stdout, out string error)
        {
            // quotepathを切らないと非ASCIIのパスが\xxx形式へ化け、未追跡ファイルのコピー元を見失う
            // Without disabling quotepath, non-ASCII paths come back escaped and the untracked copy loses its source
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "-c core.quotepath=false " + arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            // 外部プロセス起動は例外を返す境界のため、ここに限りcatchして失敗理由へ変換する
            // Process spawning is an external boundary; only here we catch and convert failures into a reason
            try
            {
                using var process = Process.Start(startInfo);

                // stderrを先に非同期で排水する。stdoutだけ読むと64KB超のstderrで子がwriteブロックしハングする
                // Drain stderr asynchronously first; reading only stdout hangs the child once stderr exceeds the 64KB pipe
                var stderrTask = process.StandardError.ReadToEndAsync();
                stdout = process.StandardOutput.ReadToEnd();
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
