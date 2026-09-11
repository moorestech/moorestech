using System;
using Client.Game.InGame.BugReport;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    // BuildInfoWriter が焼き込むJSONの組み立てを検証する（Editorアセンブリを参照できないため組み立て関数を直接叩く）
    // Verifies the JSON BuildInfoWriter bakes in, calling the composer directly since the Editor assembly is unreferenced
    public class BuildInfoWriterTest
    {
        [Test]
        public void 生成JSONは必要なキーを持つ()
        {
            var repo = new RepositoryProbeResult { State = new RepositoryState { Commit = "a", Branch = "b", Dirty = true } };
            var master = new RepositoryProbeResult { State = new RepositoryState { Commit = "c", Branch = "HEAD", Dirty = false } };
            var json = JObject.Parse(RepositoryStateProbe.ComposeBuildInfoJson(repo, master, new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc)));
            Assert.AreEqual("a", (string)json["commit"]);
            Assert.AreEqual("b", (string)json["branch"]);
            Assert.AreEqual(true, (bool)json["dirty"]);
            Assert.AreEqual("c", (string)json["masterCommit"]);
            Assert.AreEqual(false, (bool)json["masterDirty"]);
            Assert.AreEqual("2026-09-11T00:00:00Z", (string)json["builtAt"]);
        }

        // git が無い機械でもビルドは通す。取れなかった状態は空文字として焼き込まれる
        // A machine without git still builds; an unavailable state is baked in as an empty string
        [Test]
        public void 状態が取れなかった場合も有効なJSONになる()
        {
            var failed = new RepositoryProbeResult { Error = "git を起動できない" };
            var json = JObject.Parse(RepositoryStateProbe.ComposeBuildInfoJson(failed, failed, DateTime.UtcNow));
            Assert.AreEqual("", (string)json["commit"]);
            Assert.AreEqual("", (string)json["masterCommit"]);
            Assert.AreEqual(false, (bool)json["dirty"]);
        }
    }
}
