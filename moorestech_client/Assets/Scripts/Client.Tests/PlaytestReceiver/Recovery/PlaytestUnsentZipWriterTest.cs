using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Client.PlaytestReceiver.BoxFilePolicies;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.PlaytestReceiver.Upload;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Recovery
{
    public class PlaytestUnsentZipWriterTest
    {
        private string _root;
        private PlaytestOutboxDirectories _directories;
        private string _zip;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "export-" + Path.GetRandomFileName());
            _directories = new PlaytestOutboxDirectories(Path.Combine(_root, "reports"), new BugReportBoxFilePolicy(), Path.Combine(_root, "progress"), new ProgressRecordBoxFilePolicy());
            _zip = Path.Combine(_root, "out.zip");
        }

        [TearDown]
        public void TearDown() { Directory.Delete(_root, true); }

        [Test]
        public void IncludesPendingAndFailedButNotDelivered()
        {
            MakeBox("reports", "pending1", "READY");
            MakeBox("reports", "failed1", "READY", "UPLOAD_FAILED");
            MakeBox("reports", "sent1", "READY", "UPLOADED");
            MakeBox("progress", "progress1", "READY");

            var result = PlaytestUnsentZipWriter.Write(_directories, PlaytestOutboxScope.ReportsAndProgress, new FileStream(_zip, FileMode.CreateNew), new DateTime(2026, 10, 27, 11, 0, 0, DateTimeKind.Utc));

            Assert.AreEqual(3, result.BoxCount);
            CollectionAssert.IsEmpty(result.Missing);
            using var archive = ZipFile.OpenRead(_zip);
            var names = archive.Entries.Select(e => e.FullName).ToList();
            CollectionAssert.Contains(names, "reports/pending1/payload.txt");
            CollectionAssert.Contains(names, "reports/failed1/UPLOAD_FAILED");
            CollectionAssert.Contains(names, "progress/progress1/payload.txt");
            Assert.IsFalse(names.Any(n => n.StartsWith("reports/sent1/")));
            Assert.IsFalse(names.Any(n => n.EndsWith("/UPLOAD_LOCK")));
            // 日時文字列をDateTimeへ変換させず、書かれた文字そのものを検証する
            // Keep the date as written instead of letting Json.NET turn it into DateTime
            var meta = JsonConvert.DeserializeObject<JObject>(new StreamReader(archive.GetEntry("export.json").Open()).ReadToEnd(), new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
            Assert.AreEqual("2026-10-27T11:00:00Z", (string)meta["exportedAt"]);
            Assert.AreEqual(3, ((JArray)meta["boxes"]).Count);
        }

        [Test]
        public void ReportsOnlyScopeSkipsProgress()
        {
            MakeBox("reports", "r1", "READY");
            MakeBox("progress", "g1", "READY");
            var result = PlaytestUnsentZipWriter.Write(_directories, PlaytestOutboxScope.ReportsOnly, new FileStream(_zip, FileMode.CreateNew), DateTime.UtcNow);
            Assert.AreEqual(1, result.BoxCount);
        }

        [Test]
        public void UnreadableFileIsListedAsMissingAndOthersStay()
        {
            var box = MakeBox("reports", "r1", "READY");
            var locked = Path.Combine(box, "locked.bin");
            File.WriteAllText(locked, "x");
            // 排他で開いたままにして読めないファイルを作る（Windowsの他プロセスのロックと同じ失敗）
            // Hold the file open exclusively so reading fails, as another process's lock would on Windows
            using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("export skipped"));
                var result = PlaytestUnsentZipWriter.Write(_directories, PlaytestOutboxScope.ReportsOnly, new FileStream(_zip, FileMode.CreateNew), DateTime.UtcNow);
                Assert.AreEqual(1, result.BoxCount);
                Assert.AreEqual("reports/r1/locked.bin", result.Missing.Single().Item);
            }
            using var archive = ZipFile.OpenRead(_zip);
            Assert.IsNotNull(archive.GetEntry("reports/r1/payload.txt"));
        }

        private string MakeBox(string outbox, string id, params string[] markers)
        {
            var box = Directory.CreateDirectory(Path.Combine(_root, outbox, id)).FullName;
            File.WriteAllText(Path.Combine(box, "payload.txt"), "x");
            File.WriteAllText(Path.Combine(box, "UPLOAD_LOCK"), "");
            foreach (var marker in markers) File.WriteAllText(Path.Combine(box, marker), "reason");
            return box;
        }
    }
}
