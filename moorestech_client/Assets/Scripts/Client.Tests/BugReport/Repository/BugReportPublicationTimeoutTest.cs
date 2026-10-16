using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Client.Game.InGame.BugReport;
using NUnit.Framework;

namespace Client.Tests.BugReport.Repository
{
    public class BugReportPublicationTimeoutTest
    {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void UnresponsiveSshStillCompletesEvidence(bool legacy, bool relay)
        {
            if (relay && Environment.OSVersion.Platform == PlatformID.Win32NT)
                Assert.Ignore("SSH relay fixture requires Unix /usr/bin/ssh");
            var originalPath = Environment.GetEnvironmentVariable("PATH");
            var root = Path.Combine(Path.GetTempPath(), "bugreport-timeout-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var relayPidPath = Path.Combine(root, "relay.pid");
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                Git(root, "init");
                Git(root, "-c user.name=Test -c user.email=test@example.invalid commit --allow-empty -m initial");
                var commit = Git(root, "rev-parse HEAD").Trim();
                Git(root, $"update-ref refs/remotes/origin/master {commit}");
                Git(root, $"remote add origin ssh://git@127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/repo");
                var manifest = new BugReportManifest { IsEditor = true, Repository = new RepositoryState { Commit = commit } };
                var box = Path.Combine(root, "box");
                Directory.CreateDirectory(box);
                var accepted = listener.AcceptTcpClientAsync();
                // 中継SSHを孫プロセスにして、直下だけの終了では残る接続を検証する
                // Make the relay SSH a grandchild to detect cleanup that only kills direct children
                if (relay)
                {
                    var bin = Path.Combine(root, "bin");
                    Directory.CreateDirectory(bin);
                    var wrapper = Path.Combine(bin, "ssh");
                    var proxy = Path.Combine(bin, "proxy");
                    File.WriteAllText(proxy, $"echo $$ > '{relayPidPath}'\nexec /usr/bin/ssh -F /dev/null -oBatchMode=yes -W ignored:22 -p " +
                        ((IPEndPoint)listener.LocalEndpoint).Port + " git@127.0.0.1\n");
                    File.WriteAllText(wrapper, $"#!/bin/sh\nexec /usr/bin/ssh -F /dev/null -o 'ProxyCommand=/bin/sh {proxy}' \"$@\"\n");
                    using var chmod = Process.Start("/bin/chmod", $"+x \"{wrapper}\"");
                    Assert.That(chmod.WaitForExit(2000), Is.True);
                    Assert.That(chmod.ExitCode, Is.Zero);
                    Environment.SetEnvironmentVariable("PATH", bin + Path.PathSeparator + originalPath);
                }

                // TCP接続だけ受理してSSH応答を返さず、実際の公開確認を期限切れにする
                // Accept TCP without an SSH response to time out the actual publication check
                var timer = Stopwatch.StartNew();
                if (legacy)
                {
                    var master = Path.Combine(root, "master");
                    Directory.CreateDirectory(master);
                    Git(master, "init");
                    Git(master, "-c user.name=Test -c user.email=test@example.invalid commit --allow-empty -m master");
                    manifest.MasterData = new RepositoryState { Commit = Git(master, "rev-parse HEAD").Trim() };
                    File.WriteAllText(Path.Combine(box, "manifest.json"), manifest.ToJson());
                    Assert.IsNull(BugReportCommitBundles.CompleteExisting(box, root, master, Path.Combine(box, "UPLOAD_DECLARED")));
                }
                else BugReportCommitBundles.Write(Path.Combine(box, "repo"), "timeout", manifest, root, null);
                Assert.That(timer.Elapsed.TotalSeconds, Is.LessThan(22));
                Assert.That(accepted.IsCompleted, Is.True);
                Assert.That(File.ReadAllText(Path.Combine(box, "repo/bundle-status.txt")), Does.Contain("commits.bundle: created"));
                Assert.That(manifest.Missing, Is.Empty);

                // transportの切断と、空repoへ復元できる資料の完成を確認する
                // Verify transport closure and completed evidence restorable into an empty repository
                using var client = accepted.Result;
                client.ReceiveTimeout = 2000;
                using var stream = client.GetStream();
                while (stream.ReadByte() != -1) { }
                if (relay)
                {
                    var relayPid = int.Parse(File.ReadAllText(relayPidPath).Trim());
                    foreach (var running in Process.GetProcesses())
                    {
                        using (running) Assert.That(running.Id, Is.Not.EqualTo(relayPid), "relay SSH survived timeout");
                    }
                }
                var imported = Path.Combine(root, "import");
                Directory.CreateDirectory(imported);
                Git(imported, "init");
                Git(imported, $"fetch \"{Path.Combine(box, "repo/commits.bundle")}\" +refs/bugreport/*:refs/bugreport/*");
                Assert.AreEqual(commit, Git(imported, "rev-parse FETCH_HEAD").Trim());
                Assert.That(Git(root, "for-each-ref refs/bugreport"), Is.Empty);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", originalPath);
                // 失敗した試験でも、自分の中継PIDを回収する
                // Reap our recorded relay PID even when the regression fails
                if (File.Exists(relayPidPath))
                {
                    var relayPid = int.Parse(File.ReadAllText(relayPidPath).Trim());
                    foreach (var running in Process.GetProcesses())
                    {
                        using (running)
                            if (running.Id == relayPid) { running.Kill(); running.WaitForExit(2000); }
                    }
                }
                listener.Stop();
                Directory.Delete(root, true);
            }
        }

        private static string Git(string root, string arguments)
        {
            using var process = Process.Start(new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = root, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true,
            });
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            Assert.That(process.WaitForExit(5000), Is.True, arguments);
            Assert.That(process.ExitCode, Is.Zero, error.Result);
            return output.Result;
        }
    }
}
