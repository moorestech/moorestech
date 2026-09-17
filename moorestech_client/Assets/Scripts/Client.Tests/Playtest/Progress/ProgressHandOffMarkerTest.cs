using System;
using System.IO;
using System.Text.RegularExpressions;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.Playtest.Progress;
using Client.Game.InGame.Playtest.Progress.Record;
using Client.Game.InGame.Playtest.Progress.Record.Events;
using Client.Game.InGame.Playtest.Progress.Storage;
using Game.Paths;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Playtest
{
    // READY と current/ の削除の間で落ちた残骸を、次回起動が二重に送らないことを固定する（F18）
    // Pins that a leftover which crashed between READY and clearing current/ is never shipped twice by the next boot (F18)
    public class ProgressHandOffMarkerTest
    {
        private const string FakeBundleName = "20260915_000000_handoff1";
        private static readonly MissingItem[] NoExtraMissing = Array.Empty<MissingItem>();

        private static string FakeBundle => Path.Combine(GameSystemPaths.ProgressRecordOutboxDirectory, FakeBundleName);

        [SetUp]
        [TearDown]
        public void Clear()
        {
            ProgressTestSession.Clear();
            if (Directory.Exists(FakeBundle)) Directory.Delete(FakeBundle, true);
        }

        private static void WriteLeftoverHandedTo(string bundleDirectory)
        {
            ProgressTestSession.WriteHeader(new ProgressRecordHeader { SessionStart = ProgressUtcTime.ToIso(DateTime.UtcNow.AddMinutes(-1)) });
            ProgressTestSession.AppendEvent(new BlockPlacedEvent(DateTime.UtcNow, 1, 1));
            Assert.IsTrue(ProgressHandOffMarker.Write(ProgressTestSession.Directory, bundleDirectory).Succeeded);
        }

        [Test]
        public void 受け渡し印の箱がREADYなら箱を作り直さずcurrentだけ片付ける()
        {
            Directory.CreateDirectory(FakeBundle);
            File.WriteAllText(Path.Combine(FakeBundle, BugReportOutbox.ReadyMarkerFileName), "");
            WriteLeftoverHandedTo(FakeBundle);
            var bundleCountBefore = Directory.GetDirectories(GameSystemPaths.ProgressRecordOutboxDirectory).Length;

            var result = ProgressRecordFiles.CloseLeftoverInto(ProgressTestSession.Directory, ProgressEndReason.CrashRecovered, NoExtraMissing);

            Assert.AreEqual(FakeBundle, result.BundleDirectory, "渡し済みの箱を指していない");
            Assert.AreEqual(bundleCountBefore, Directory.GetDirectories(GameSystemPaths.ProgressRecordOutboxDirectory).Length, "渡し済みの記録から箱を作り直している");
            Assert.IsFalse(ProgressTestSession.HasCurrentSession(), "渡し済みの current/ が片付いていない");
        }

        // READY の無い箱を指す印は渡し損ね。ここで片付けると記録が1件も運搬されずに消える
        // A mark pointing at a box without READY is a failed hand-off; clearing here would lose the record without ever shipping it
        [Test]
        public void 受け渡し印の箱にREADYが無ければ回収し直す()
        {
            Directory.CreateDirectory(FakeBundle);
            WriteLeftoverHandedTo(FakeBundle);

            LogAssert.Expect(LogType.Warning, new Regex("READYが無い"));
            var result = ProgressRecordFiles.CloseLeftoverInto(ProgressTestSession.Directory, ProgressEndReason.CrashRecovered, NoExtraMissing);

            Assert.IsNotNull(result.BundleDirectory);
            Assert.AreNotEqual(FakeBundle, result.BundleDirectory, "渡し損ねの箱を渡し済みとして扱っている");
            Assert.IsTrue(File.Exists(Path.Combine(result.BundleDirectory, BugReportOutbox.ReadyMarkerFileName)));
            Directory.Delete(result.BundleDirectory, true);
        }
    }
}
