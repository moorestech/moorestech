using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Client.Editor.RepositorySync
{
    public static class ExternalRepositorySyncService
    {
        public static void SyncToRecordedCommits()
        {
            var revisionFile = ExternalRepositoryRevisionStore.Load();
            var repositoryRoot = ExternalRepositoryRevisionStore.GetRepositoryRootPath();
            var assetRepositoryChanged = false;

            foreach (var repository in revisionFile.repositories)
            {
                if (SyncRepository(repositoryRoot, repository))
                {
                    assetRepositoryChanged |= IsUnderAssetsDirectory(repositoryRoot, repository.relativePath);
                }
            }

            if (assetRepositoryChanged)
            {
                AssetDatabase.Refresh();
            }
        }

        public static void RecordCurrentCommits()
        {
            var revisionFile = ExternalRepositoryRevisionStore.Load();
            var repositoryRoot = ExternalRepositoryRevisionStore.GetRepositoryRootPath();

            foreach (var repository in revisionFile.repositories)
            {
                RecordRepositoryHead(repositoryRoot, repository);
            }

            ExternalRepositoryRevisionStore.Save(revisionFile);
            Debug.Log("External repository revisions recorded.");
        }

        private static bool SyncRepository(string repositoryRoot, ExternalRepositoryRevisionEntry repository)
        {
            var repositoryPath = ResolveRepositoryPath(repositoryRoot, repository.relativePath);
            if (!ExternalRepositoryGitClient.IsGitRepository(repositoryPath))
            {
                Debug.Log($"External repository skipped because it was not found: {repository.key} ({repositoryPath})");
                return false;
            }

            if (string.IsNullOrWhiteSpace(repository.commitHash))
            {
                Debug.LogWarning($"External repository skipped because no commit is recorded: {repository.key}");
                return false;
            }

            // ローカル変更がある外部repoは自動checkoutせず作業内容を保護する
            // Preserve external repository work by skipping automatic checkout with local changes
            if (ExternalRepositoryGitClient.HasLocalChanges(repositoryPath))
            {
                Debug.LogWarning($"External repository skipped because it has local changes: {repository.key} ({repositoryPath})");
                return false;
            }

            var currentHeadCommit = ExternalRepositoryGitClient.ReadHeadCommit(repositoryPath);
            if (currentHeadCommit == repository.commitHash)
            {
                Debug.Log($"External repository already matches recorded commit: {repository.key}");
                return false;
            }

            // 対象commitを取得してからdetached HEADで正確なrevisionへ移動する
            // Fetch the target commit first, then move to the exact revision with detached HEAD
            ExternalRepositoryGitClient.Fetch(repositoryPath);
            if (!ExternalRepositoryGitClient.ContainsCommit(repositoryPath, repository.commitHash))
            {
                Debug.LogWarning($"External repository skipped because commit was not found: {repository.key} ({repository.commitHash})");
                return false;
            }

            if (ExternalRepositoryGitClient.Checkout(repositoryPath, repository.commitHash))
            {
                Debug.Log($"External repository checked out: {repository.key} {repository.commitHash}");
                return true;
            }

            return false;
        }

        private static void RecordRepositoryHead(string repositoryRoot, ExternalRepositoryRevisionEntry repository)
        {
            var repositoryPath = ResolveRepositoryPath(repositoryRoot, repository.relativePath);
            if (!ExternalRepositoryGitClient.IsGitRepository(repositoryPath))
            {
                Debug.Log($"External repository record skipped because it was not found: {repository.key} ({repositoryPath})");
                return;
            }

            var headCommit = ExternalRepositoryGitClient.ReadHeadCommit(repositoryPath);
            if (string.IsNullOrWhiteSpace(headCommit))
            {
                Debug.LogWarning($"External repository record skipped because HEAD could not be read: {repository.key}");
                return;
            }

            repository.commitHash = headCommit;
            Debug.Log($"External repository recorded: {repository.key} {headCommit}");
        }

        private static string ResolveRepositoryPath(string repositoryRoot, string relativePath)
        {
            return Path.GetFullPath(Path.Combine(repositoryRoot, relativePath));
        }

        private static bool IsUnderAssetsDirectory(string repositoryRoot, string relativePath)
        {
            var repositoryPath = ResolveRepositoryPath(repositoryRoot, relativePath);
            var assetsPath = Path.GetFullPath(Path.Combine(repositoryRoot, "moorestech_client", "Assets"));
            var assetsPathWithSeparator = assetsPath + Path.DirectorySeparatorChar;

            return repositoryPath == assetsPath || repositoryPath.StartsWith(assetsPathWithSeparator, StringComparison.Ordinal);
        }
    }
}
