using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Support
{
    /// <summary>
    ///     コミット済みpinが指すmoorestech_masterのコミットからファイルを読む
    ///     Reads files from the moorestech_master commit named by the committed pin
    /// </summary>
    public static class PinnedMasterRepository
    {
        private const string MasterRepositoryKey = "moorestech_master";
        private const int GitExitTimeoutMilliseconds = 5000;
        private const int GitStreamTimeoutMilliseconds = 2000;

        public static string ReadPinnedFile(string pathInMasterRepository)
        {
            // 作業ツリーのピンはUnityが実チェックアウト値へ書き戻すので、コミット済みの値だけを信じる
            // Unity rewrites the working-tree pin to the resolved checkout, so only the committed value is trusted
            var repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var revisionJson = RunGit(repositoryRoot, "show HEAD:.moorestech-external-revisions.json");
            var revision = FindMasterRevision(JObject.Parse(revisionJson));

            // worktreeから起動されても本体repoの隣を見るため、共通gitディレクトリで正本を特定する
            // Locate the primary repo via the common git directory so worktrees resolve the same neighbour
            var commonGitDirectory = RunGit(repositoryRoot, "rev-parse --path-format=absolute --git-common-dir").Trim();
            var primaryRepositoryRoot = Directory.GetParent(commonGitDirectory).FullName;
            var masterRepositoryRoot = Path.GetFullPath(Path.Combine(primaryRepositoryRoot, (string)revision["relativePath"]));
            Assert.IsTrue(Directory.Exists(masterRepositoryRoot), $"Pinned master repository not found: {masterRepositoryRoot}");

            return RunGit(masterRepositoryRoot, $"show {(string)revision["commitHash"]}:{pathInMasterRepository}");
        }

        private static JObject FindMasterRevision(JObject revisionRoot)
        {
            foreach (var token in (JArray)revisionRoot["repositories"])
            {
                var revision = (JObject)token;
                if ((string)revision["key"] == MasterRepositoryKey) return revision;
            }

            Assert.Fail($"Committed external revisions do not contain {MasterRepositoryKey}");
            return null;
        }

        public static string RunGit(string workingDirectory, string arguments)
        {
            // CIコンテナはworkspace所有者が異なりdubious ownershipでexit 128になるため、プロセス限定で信頼する
            // CI containers own the workspace as another user and git exits 128 with dubious ownership, so trust it per process only
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-c safe.directory=* {arguments}",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            // 外部プロセス起動は外部境界なので、ここは有界時間の失敗検知に徹する
            // Launching an external process is a boundary, so this spot only bounds and reports the failure
            using var process = Process.Start(startInfo);
            Assert.IsNotNull(process, $"Failed to start git in {workingDirectory}");

            // 両ストリームを先に非同期で吸い出さないと、出力が大きいときパイプ満杯で相互待ちになる
            // Draining both streams asynchronously first avoids a pipe-full deadlock on large outputs
            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(GitExitTimeoutMilliseconds))
            {
                process.Kill();
                Assert.Fail($"git timed out in {workingDirectory}: {arguments}");
            }

            var outputTasks = new Task[] { standardOutputTask, standardErrorTask };
            Assert.IsTrue(Task.WaitAll(outputTasks, GitStreamTimeoutMilliseconds), "git output streams did not close");
            Assert.AreEqual(0, process.ExitCode, $"git failed: {arguments}\n{standardErrorTask.Result}");
            return standardOutputTask.Result;
        }
    }
}
