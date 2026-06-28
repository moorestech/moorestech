using System.IO;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Client.Editor.RepositorySync
{
    public static class ExternalRepositoryGitClient
    {
        public static bool IsGitRepository(string repositoryPath)
        {
            if (!Directory.Exists(repositoryPath)) return false;

            var dotGitPath = Path.Combine(repositoryPath, ".git");
            return Directory.Exists(dotGitPath) || File.Exists(dotGitPath);
        }

        public static string ReadHeadCommit(string repositoryPath)
        {
            var result = Run(repositoryPath, "rev-parse HEAD");
            if (result.exitCode != 0) return "";

            return result.standardOutput.Trim();
        }

        public static bool HasLocalChanges(string repositoryPath)
        {
            var result = Run(repositoryPath, "status --porcelain");
            if (result.exitCode != 0) return true;

            return !string.IsNullOrWhiteSpace(result.standardOutput);
        }

        public static bool ContainsCommit(string repositoryPath, string commitHash)
        {
            var result = Run(repositoryPath, $"cat-file -e {commitHash}^{{commit}}");
            return result.exitCode == 0;
        }

        public static bool Fetch(string repositoryPath)
        {
            var result = Run(repositoryPath, "fetch --all --tags --prune");
            if (result.exitCode == 0) return true;

            Debug.LogWarning(CreateErrorMessage(repositoryPath, "fetch", result));
            return false;
        }

        public static bool Checkout(string repositoryPath, string commitHash)
        {
            var result = Run(repositoryPath, $"checkout --detach {commitHash}");
            if (result.exitCode == 0) return true;

            Debug.LogWarning(CreateErrorMessage(repositoryPath, "checkout", result));
            return false;
        }

        public static GitCommandResult Run(string repositoryPath, string arguments)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = repositoryPath,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            // gitの出力を全て読み切ってEditor側の待機詰まりを避ける
            // Read all git output so the editor does not block on process buffers
            using var process = Process.Start(processStartInfo);
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return new GitCommandResult(process.ExitCode, standardOutput, standardError);
        }

        private static string CreateErrorMessage(string repositoryPath, string commandName, GitCommandResult result)
        {
            var message = result.standardError.Trim();
            if (string.IsNullOrEmpty(message)) message = result.standardOutput.Trim();

            return $"External repository git {commandName} failed: {repositoryPath}\n{message}";
        }
    }
}
