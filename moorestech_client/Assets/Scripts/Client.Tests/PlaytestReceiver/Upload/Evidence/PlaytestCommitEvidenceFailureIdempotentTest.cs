using System;
using System.IO;
using Client.PlaytestReceiver.BoxFilePolicies;
using Client.PlaytestReceiver.Upload;
using Client.PlaytestReceiver.Upload.Attempt;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestCommitEvidenceFailureIdempotentTest
    {
        private string _root;

        [SetUp]
        public void Initialize() { _root = Path.Combine(Path.GetTempPath(), "evidence-idem-" + Guid.NewGuid().ToString("N")); }

        [TearDown]
        public void Cleanup() { Directory.Delete(_root, true); }

        [Test]
        public void 同じ消失資料を繰り返し記録してもmissingは1行だけ()
        {
            var directory = PlaytestOutboxTestBoxes.Make(_root, "20261016_120000_aaaa", ("manifest.json", "{\"isEditor\":false,\"missing\":[]}"));
            Directory.CreateDirectory(Path.Combine(directory, "repo"));
            File.WriteAllText(Path.Combine(directory, "repo/bundle-status.txt"), "commits.bundle: created\nmaster-commits.bundle: skipped-no-commit");
            var policy = new BugReportBoxFilePolicy();
            for (var i = 0; i < 2; i++)
            {
                var box = new PlaytestOutboxBox(directory, Path.GetFileName(directory), Client.PlaytestReceiver.Http.PlaytestUploadKind.Report, policy);
                UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("commit evidence cannot be delivered"));
                Assert.IsNotNull(policy.ValidateBeforeUpload(directory, PlaytestBoxDeclaration.Build(box)));
            }
            var missing = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")))["missing"];
            Assert.AreEqual(1, missing.Count);
        }
    }
}
