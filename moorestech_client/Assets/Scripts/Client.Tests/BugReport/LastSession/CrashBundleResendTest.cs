using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Game.Paths;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BugReport
{
    // 書き出しに失敗したあとの再送で、退避物が失われず「中身の無い箱」が正式に出荷されないことを押さえる（ADR 0060 裁定1）
    // Pins that a resend after a failed write neither loses the salvage nor officially ships an empty box (ADR 0060 adjudication 1)
    public class CrashBundleResendTest
    {
        // 1回目の書き出しで退避元が空になった状態での再送。空を素通りさせると、証跡の無い箱がREADY付きで正式に出荷される
        // A resend after the first write emptied the source; letting the emptiness pass would officially ship an evidence-free box with READY on it
        [Test]
        public async Task 退避元が空のまま再送すると欠損として表明される()
        {
            var source = Path.Combine(Path.GetTempPath(), $"moorestech-crash-{Guid.NewGuid():N}");
            var recording = Path.Combine(source, "recording");
            Directory.CreateDirectory(recording);

            var artifacts = TestPreviousSessionArtifacts.UncleanWithRecording(recording);
            var bundle = await new CrashBundleWriter().WriteAsync(artifacts, "再送");

            try
            {
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, BugReportBundleLayout.ManifestFileName)));
                var missing = (JArray)manifest["missing"];
                Assert.IsTrue(missing.Any(item => (string)item["item"] == BugReportBundleLayout.RecordingDirectoryName), "空の退避元が欠損として表明されていない");
            }
            finally
            {
                Directory.Delete(bundle, true);
                Directory.Delete(source, true);
            }
        }

        // 箱を閉じられなかったときは退避物を last-session へ戻す。戻さないと「送り直せる」（ADR 0060 裁定1）が成立しない
        // A box that could not be closed gives the salvage back to last-session; without it "you can resend" (ADR 0060 adjudication 1) does not hold
        [Test]
        public void 閉じられなかった箱の退避物はlastSessionへ戻る()
        {
            var root = Path.Combine(Path.GetTempPath(), $"moorestech-crash-{Guid.NewGuid():N}");
            var salvage = Path.Combine(root, "last-session", "recording");
            var unfinished = Path.Combine(root, "box", BugReportBundleLayout.RecordingDirectoryName, "pid_1234");
            Directory.CreateDirectory(salvage);
            Directory.CreateDirectory(unfinished);
            File.WriteAllText(Path.Combine(unfinished, "segment-0.mp4"), "video");

            var artifacts = TestPreviousSessionArtifacts.UncleanWithRecording(salvage);

            try
            {
                CrashBundleSalvageMover.RestoreSalvageFromUnfinishedBundle(Path.Combine(root, "box"), artifacts);
                Assert.IsTrue(File.Exists(Path.Combine(salvage, "pid_1234", "segment-0.mp4")), "退避物が last-session へ戻っていない");
                Assert.IsFalse(Directory.Exists(Path.Combine(root, "box")), "全件戻したのに未完成の箱が残っている");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        // 戻しが途中で失敗すると退避物は箱と退避元に分断される。欠損として表明しないと、片肺の箱がREADY付きで出荷される
        // A restore that fails midway splits the salvage between the box and the source; without declaring the gap a half-filled box ships with READY on it
        [Test]
        public void 戻せなかった退避物は欠損として表明される()
        {
            var root = Path.Combine(Path.GetTempPath(), $"moorestech-crash-{Guid.NewGuid():N}");
            var salvage = Path.Combine(root, "last-session", "recording");
            var unfinished = Path.Combine(root, "box", BugReportBundleLayout.RecordingDirectoryName, "pid_1234");
            Directory.CreateDirectory(Path.Combine(salvage, "pid_1234"));
            Directory.CreateDirectory(unfinished);
            File.WriteAllText(Path.Combine(unfinished, "segment-0.mp4"), "video");

            // 戻し先に同名ファイルが居ると File.Move が IOException を投げる。ロックや権限と同じ経路をテストから起こせる唯一の手
            // A same-named file at the destination makes File.Move throw IOException, the only way a test can take the same path as a lock or a permission refusal
            File.WriteAllText(Path.Combine(salvage, "pid_1234", "segment-0.mp4"), "already there");

            var artifacts = TestPreviousSessionArtifacts.UncleanWithRecording(salvage);

            try
            {
                LogAssert.Expect(LogType.Error, new Regex("退避物を last-session へ戻せませんでした"));
                CrashBundleSalvageMover.RestoreSalvageFromUnfinishedBundle(Path.Combine(root, "box"), artifacts);

                Assert.IsTrue(artifacts.Missing.Any(item => item.Item == BugReportBundleLayout.RecordingDirectoryName), "戻せなかった退避物が欠損として積まれていない");
                Assert.IsTrue(Directory.Exists(Path.Combine(root, "box")), "戻し切れていないのに未完成の箱が消えている");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
