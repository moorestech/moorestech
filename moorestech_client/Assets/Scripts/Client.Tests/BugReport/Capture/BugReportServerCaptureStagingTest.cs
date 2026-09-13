using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport.Capture;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BugReport.Capture
{
    // Escape時点の記録を実体で確保できているかの回帰ガード。名前だけの確保では記入中の剪定で消える
    // Regression guard that the Escape-moment records are secured as files; names alone vanish to pruning while the user types
    public class BugReportServerCaptureStagingTest
    {
        private string _snapshotDirectory;
        private string _stagingDirectory;

        [SetUp]
        public void SetUp()
        {
            var root = Path.Combine(Path.GetTempPath(), "bugreport_staging_test_" + Path.GetRandomFileName());
            _snapshotDirectory = Path.Combine(root, "snapshots");
            _stagingDirectory = Path.Combine(root, "staging");
            Directory.CreateDirectory(_snapshotDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            var root = Path.GetDirectoryName(_snapshotDirectory);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void 確保のあとにサーバーが剪定しても退避した実体は残る()
        {
            File.WriteAllText(Path.Combine(_snapshotDirectory, "tick_5.json"), "{\"tick\":5}");
            File.WriteAllText(Path.Combine(_snapshotDirectory, "packets_6.bin"), "packets");

            var staged = BugReportServerCaptureStaging.Stage(_stagingDirectory, _snapshotDirectory, new[] { "tick_5.json" }, new[] { "packets_6.bin" });

            // 記入中の剪定を再現する。退避が名前だけなら、ここで送信時に読む実体が消える
            // Reproduces the pruning that happens while typing; with names-only staging the file a send would read is gone here
            Directory.Delete(_snapshotDirectory, true);

            Assert.AreEqual(_stagingDirectory, staged.StagingDirectory);
            CollectionAssert.AreEqual(new[] { "tick_5.json" }, staged.SnapshotFileNames);
            CollectionAssert.AreEqual(new[] { "packets_6.bin" }, staged.PacketLogFileNames);
            Assert.AreEqual(0, staged.Missing.Count);
            Assert.AreEqual("{\"tick\":5}", File.ReadAllText(Path.Combine(_stagingDirectory, "tick_5.json")));
            Assert.AreEqual("packets", File.ReadAllText(Path.Combine(_stagingDirectory, "packets_6.bin")));
        }

        [Test]
        public void 確保の時点で既に消えていたファイルは理由付きで欠損に載る()
        {
            File.WriteAllText(Path.Combine(_snapshotDirectory, "tick_5.json"), "{}");

            var staged = BugReportServerCaptureStaging.Stage(_stagingDirectory, _snapshotDirectory, new[] { "tick_5.json", "tick_4.json" }, new List<string>());

            CollectionAssert.AreEqual(new[] { "tick_5.json" }, staged.SnapshotFileNames);
            Assert.AreEqual("tick_4.json", staged.Missing.Single().Item);
            Assert.IsNotEmpty(staged.Missing.Single().Reason);
        }

        [Test]
        public void 置き場が分からなければ退避先を作らず理由を残す()
        {
            var staged = BugReportServerCaptureStaging.Stage(_stagingDirectory, null, new List<string>(), new List<string>());

            Assert.IsNull(staged.StagingDirectory);
            Assert.AreEqual("snapshots", staged.Missing.Single().Item);
            Assert.IsFalse(Directory.Exists(_stagingDirectory));
        }

        // 前回の確保の退避は次の確保で消す。残すと古い報告のスナップショットが新しい箱へ混ざる
        // The previous capture's staging is cleared by the next one; keeping it would mix an old report's snapshots into a new box
        [Test]
        public void 次の確保は前回の退避を消してから書く()
        {
            File.WriteAllText(Path.Combine(_snapshotDirectory, "tick_5.json"), "{}");
            Directory.CreateDirectory(_stagingDirectory);
            File.WriteAllText(Path.Combine(_stagingDirectory, "tick_1.json"), "old");

            BugReportServerCaptureStaging.Stage(_stagingDirectory, _snapshotDirectory, new[] { "tick_5.json" }, new List<string>());

            Assert.IsFalse(File.Exists(Path.Combine(_stagingDirectory, "tick_1.json")));
            Assert.IsTrue(File.Exists(Path.Combine(_stagingDirectory, "tick_5.json")));
        }

        [Test]
        public void 退避先を作れないときはエラーログを出して丸ごと欠損にする()
        {
            // 退避先と同じ名前のファイルを置き、ディレクトリを作れない状態を作る
            // Places a file where the staging directory must go so it cannot be created
            File.WriteAllText(_stagingDirectory, "not a directory");

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("退避先を用意できませんでした"));
            var staged = BugReportServerCaptureStaging.Stage(_stagingDirectory, _snapshotDirectory, new[] { "tick_5.json" }, new List<string>());

            Assert.IsNull(staged.StagingDirectory);
            Assert.AreEqual("snapshots", staged.Missing.Single().Item);
        }
    }
}
