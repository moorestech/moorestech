using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Train.Network.Diagnostics;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Diagnostics;
using NUnit.Framework;

namespace Client.Tests.BugReport.Diagnostics
{
    public class TrainSyncDiagnosticsCopierTest
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../.superpowers/task7-tests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, true);
        }

        [Test]
        public void 再生を越えると診断ディレクトリを分離し同じ再生では共有する()
        {
            var initialize = typeof(TrainSynchronizationDiagnosticDirectory).GetMethod("RecordPlayStart", BindingFlags.NonPublic | BindingFlags.Static);
            initialize.Invoke(null, null);
            var first = TrainSynchronizationDiagnosticDirectory.Resolve();
            Assert.AreEqual(first, TrainSynchronizationDiagnosticDirectory.Resolve());
            initialize.Invoke(null, null);
            var second = TrainSynchronizationDiagnosticDirectory.Resolve();
            Assert.AreNotEqual(first, second);
            Assert.AreEqual(Path.GetDirectoryName(first), Path.GetDirectoryName(second));
        }

        [Test]
        public void CopiesNewestEightFromCurrentPlayAndDeclaresTruncation()
        {
            var start = DateTime.UtcNow.AddMinutes(-1);
            var source = Path.Combine(_root, "source");
            Directory.CreateDirectory(source);
            Write("stall-old.json", start.AddSeconds(-1));
            for (var index = 0; index < 10; index++) Write($"stall-{index}.json", start.AddSeconds(index));
            Write("other.json", start.AddSeconds(20));
            var manifest = new BugReportManifest();
            TrainSyncDiagnosticsCopier.Copy(source, Path.Combine(_root, "box"), manifest);
            var copied = Directory.GetFiles(Path.Combine(_root, "box/logs/train-sync")).Select(Path.GetFileName).ToArray();
            Assert.That(copied, Is.EquivalentTo(Enumerable.Range(2, 8).Select(index => $"stall-{index}.json")));
            foreach (var name in copied)
                CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(source, name)), File.ReadAllBytes(Path.Combine(_root, "box/logs/train-sync", name)));
            Assert.That(manifest.Missing.Count, Is.EqualTo(1));
            Assert.That(manifest.Missing[0].Reason, Does.Contain("全11件"));

            void Write(string name, DateTime modified)
            {
                var path = Path.Combine(source, name);
                File.WriteAllText(path, "{\"WaitingReason\":\"" + name + "\"}");
                File.SetLastWriteTimeUtc(path, modified);
            }
        }

        [Test]
        public void 同じ再生の時計巻戻しでも古いmtimeの本文を保持する()
        {
            var source = Path.Combine(_root, "source");
            Directory.CreateDirectory(source);
            var path = Path.Combine(source, "stall-clock.json");
            File.WriteAllText(path, "{\"WaitingReason\":\"clock-rollback\"}");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-1));
            var manifest = new BugReportManifest();
            TrainSyncDiagnosticsCopier.Copy(source, Path.Combine(_root, "box"), manifest);
            CollectionAssert.AreEqual(File.ReadAllBytes(path), File.ReadAllBytes(Path.Combine(_root, "box/logs/train-sync/stall-clock.json")));
            Assert.That(manifest.Missing, Is.Empty);
        }

        [Test]
        public void AbsentDirectoryLeavesNoMissingOrDestination()
        {
            var manifest = new BugReportManifest();
            TrainSyncDiagnosticsCopier.Copy(Path.Combine(_root, "absent"), Path.Combine(_root, "box"), manifest);
            Assert.That(manifest.Missing, Is.Empty);
            Assert.That(Directory.Exists(Path.Combine(_root, "box")), Is.False);
        }

        [Test]
        public void CopyFailureIsDeclaredMissing()
        {
            var source = Path.Combine(_root, "source");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "stall-a.json"), "{}");
            var box = Path.Combine(_root, "box");
            File.WriteAllText(box, "blocks directory creation");
            var manifest = new BugReportManifest();
            TrainSyncDiagnosticsCopier.Copy(source, box, manifest);
            Assert.That(manifest.Missing.Count, Is.EqualTo(1));
            Assert.That(manifest.Missing[0].Item, Is.EqualTo("logs/train-sync/stall-a.json"));
        }
    }
}
