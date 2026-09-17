using System;
using System.IO;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.BuildOrigin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    // BuildInfoWriter が焼き込むJSONの組み立てを検証する（Editorアセンブリを参照できないため組み立て関数を直接叩く）
    // Verifies the JSON BuildInfoWriter bakes in, calling the composer directly since the Editor assembly is unreferenced
    public class BuildInfoWriterTest
    {
        private const string RepoCommit = "1111111111111111111111111111111111111111";
        private const string MasterCommit = "2222222222222222222222222222222222222222";
        private const string OtherCommit = "3333333333333333333333333333333333333333";

        [Test]
        public void 生成JSONは共有契約の全キーを持ち読み手でラウンドトリップできる()
        {
            var json = Compose(Probe(RepoCommit, "master", true), Probe(MasterCommit, "HEAD", false), MasterCommit, "playtest-20260913-1730", true, out var failureReason);

            Assert.IsNull(failureReason, failureReason);
            var raw = ParseWithoutDateConversion(json);
            Assert.AreEqual(RepoCommit, (string)raw["commit"]);
            Assert.AreEqual("master", (string)raw["branch"]);
            Assert.AreEqual(true, (bool)raw["dirty"]);
            Assert.AreEqual(MasterCommit, (string)raw["masterDataCommit"]);
            Assert.AreEqual(false, (bool)raw["masterDirty"]);
            Assert.AreEqual("playtest-20260913-1730", (string)raw["steamBuildLabel"]);
            Assert.AreEqual("2026-09-11T00:00:00Z", (string)raw["builtAt"]);
            Assert.AreEqual("StandaloneWindows64", (string)raw["target"]);
            Assert.IsFalse(raw.ContainsKey("masterCommit"), "旧キー masterCommit が残っている");

            var info = BuildInfoJson.Parse(json);
            Assert.AreEqual(MasterCommit, info.MasterDataCommit);
            Assert.AreEqual("playtest-20260913-1730", info.SteamBuildLabel);
            Assert.AreEqual("StandaloneWindows64", info.Target);
            Assert.AreEqual(false, info.MasterDataDirty);
        }

        // git が無い機械でもCI互換（非strict）ならビルドは通す。取れなかった状態は ""・false でなくnullとして焼き込まれる（F02）
        // A machine without git still builds when not strict; an unavailable state is baked in as null rather than "" or false (F02)
        [Test]
        public void 非strictでは状態が取れなかった場合もnullで有効なJSONになりビルドを止めない()
        {
            var failed = new RepositoryProbeResult { Error = "git を起動できない" };
            var json = ParseWithoutDateConversion(Compose(failed, failed, null, "", false, out var failureReason));

            Assert.IsNull(failureReason, "非strictなのにビルド失敗理由が返った");
            Assert.AreEqual(JTokenType.Null, json["commit"].Type);
            Assert.AreEqual(JTokenType.Null, json["masterDataCommit"].Type);
            Assert.AreEqual(JTokenType.Null, json["dirty"].Type, "取れなかったdirtyがfalse（クリーン）として焼かれている");
        }

        [Test]
        public void strictではgitを読めなければ理由付きでビルド失敗になる()
        {
            var failed = new RepositoryProbeResult { Error = "git を起動できない" };
            Compose(failed, Probe(MasterCommit, "HEAD", false), MasterCommit, "", true, out var failureReason);

            StringAssert.Contains("git を起動できない", failureReason);
        }

        [Test]
        public void strictではピンと実HEADが食い違えば両方のコミットを理由に出す()
        {
            Compose(Probe(RepoCommit, "master", false), Probe(MasterCommit, "HEAD", false), OtherCommit, "", true, out var failureReason);

            StringAssert.Contains(MasterCommit, failureReason);
            StringAssert.Contains(OtherCommit, failureReason);
        }

        [Test]
        public void strictではピンが読めなければビルド失敗になる()
        {
            Compose(Probe(RepoCommit, "master", false), Probe(MasterCommit, "HEAD", false), null, "", true, out var failureReason);

            Assert.IsNotNull(failureReason);
        }

        [Test]
        public void 非strictではピンずれでもビルドを止めず実HEADを焼く()
        {
            var json = ParseWithoutDateConversion(Compose(Probe(RepoCommit, "master", false), Probe(MasterCommit, "HEAD", false), OtherCommit, "", false, out var failureReason));

            Assert.IsNull(failureReason);
            Assert.AreEqual(MasterCommit, (string)json["masterDataCommit"]);
        }

        [Test]
        public void ピンファイルからmasterDataのコミットを読める()
        {
            var repositoryRoot = Path.Combine(Path.GetTempPath(), "moores-buildinfo-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(repositoryRoot);
            File.WriteAllText(Path.Combine(repositoryRoot, ".moorestech-external-revisions.json"),
                "{\"repositories\":[{\"key\":\"moorestech_master\",\"relativePath\":\"../moorestech_master\",\"commitHash\":\"" + MasterCommit + "\"}]}");

            Assert.AreEqual(MasterCommit, MasterDataRootLocator.ReadPinnedCommit(repositoryRoot));
            Directory.Delete(repositoryRoot, true);
        }

        private static string Compose(RepositoryProbeResult repo, RepositoryProbeResult master, string pinned, string label, bool isStrictBundling, out string failureReason)
        {
            var builtAt = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc);
            return BuildInfoComposer.Compose(repo, master, pinned, label, builtAt, "StandaloneWindows64", isStrictBundling, out failureReason);
        }

        private static RepositoryProbeResult Probe(string commit, string branch, bool dirty)
        {
            return new RepositoryProbeResult { State = new RepositoryState { Commit = commit, Branch = branch, Dirty = dirty } };
        }

        // 既定のJObject.ParseはISO日時文字列をDateTimeへ戻してしまい、焼き込んだ文字列そのものを検証できない
        // JObject.Parse converts ISO date strings back into DateTime by default, hiding the baked string itself
        private static JObject ParseWithoutDateConversion(string json)
        {
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
            return JObject.Load(reader);
        }
    }
}
