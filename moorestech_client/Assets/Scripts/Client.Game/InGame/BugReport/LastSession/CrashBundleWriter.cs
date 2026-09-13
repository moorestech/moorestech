using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Client.Game.InGame.BugReport.Playtest;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 退避物と説明文から kind=crash の箱を1つ書く。確保セッションが無い経路なので plan B の書き出しとは別物
    // Writes one kind=crash box from the salvaged files and the description; a path without a capture session, so it is separate from plan B's writer
    public sealed class CrashBundleWriter
    {
        private readonly IPlaytestSessionIdentity _identity;

        public CrashBundleWriter(IPlaytestSessionIdentity identity)
        {
            _identity = identity;
        }

        public string Write(PreviousSessionArtifacts artifacts, string description)
        {
            var directory = BugReportOutbox.CreateBundleDirectory(DateTime.UtcNow, Guid.NewGuid().ToString("N").Substring(0, 8));

            // build-info.json はビルドにしか焼かれない。Editorで読みに行くと不在の警告だけが出る（ADR 0059の唯一の読み手を共有する）
            // build-info.json is baked only into builds; reading it in the Editor only logs an absence warning (sharing ADR 0059's single reader)
            var manifest = new BugReportManifest
            {
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                Description = description,
                Kind = PlaytestReportKind.Crash,
                SteamId = _identity.SteamId,
                BuildInfo = Application.isEditor ? null : RepositoryStateProbe.ReadBuildInfo(),
                Platform = Application.platform.ToString(),
                IsEditor = Application.isEditor,
                Missing = new List<MissingItem>(artifacts.Missing),
            };

            CopyTree(artifacts.RecordingDirectory, Path.Combine(directory, BugReportBundleLayout.RecordingDirectoryName));
            CopySnapshots(artifacts.SnapshotsDirectory, directory, manifest);
            CopyFileInto(artifacts.PlayerLogPath, Path.Combine(directory, BugReportBundleLayout.LogsDirectoryName));
            foreach (var dump in artifacts.CrashDumpFiles) CopyFileInto(dump, Path.Combine(directory, BugReportBundleLayout.CrashDumpsDirectoryName));

            File.WriteAllText(Path.Combine(directory, BugReportBundleLayout.ManifestFileName), manifest.ToJson());
            BugReportOutbox.MarkReady(directory);
            Debug.Log($"前回異常終了の箱を書きました {directory} missing:{manifest.Missing.Count}");
            return directory;
        }

        // スナップショットとパケットログは plan B のプレイ報告と同じ snapshots/ 配下へ揃える（再現側の入口を1つに保つ）
        // Snapshots and packet logs land under the same snapshots/ as plan B's report, keeping one entry point for reproduction
        private static void CopySnapshots(string source, string bundleDirectory, BugReportManifest manifest)
        {
            var destination = Path.Combine(bundleDirectory, BugReportBundleLayout.SnapshotDirectoryName);
            foreach (var relativePath in CopyTree(source, destination))
            {
                var name = Path.GetFileName(relativePath);
                if (name.StartsWith("tick_", StringComparison.Ordinal)) manifest.SnapshotFiles.Add(relativePath);
                if (name.StartsWith("packets_", StringComparison.Ordinal)) manifest.PacketLogFiles.Add(relativePath);
            }
        }

        // 退避は pid_<PID>/ 等の入れ子を保ったまま移すため、写しも入れ子ごと辿る。戻り値は写した相対パス
        // The salvage keeps nesting such as pid_<PID>/, so the copy walks the whole tree; the relative paths copied are returned
        private static IReadOnlyList<string> CopyTree(string source, string destination)
        {
            var copied = new List<string>();
            if (source == null || !Directory.Exists(source)) return copied;

            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(source, file);
                var destinationFile = Path.Combine(destination, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
                File.Copy(file, destinationFile, true);
                copied.Add(relativePath);
            }
            return copied;
        }

        private static void CopyFileInto(string sourceFile, string destinationDirectory)
        {
            if (sourceFile == null || !File.Exists(sourceFile)) return;
            Directory.CreateDirectory(destinationDirectory);
            File.Copy(sourceFile, Path.Combine(destinationDirectory, Path.GetFileName(sourceFile)), true);
        }
    }
}
