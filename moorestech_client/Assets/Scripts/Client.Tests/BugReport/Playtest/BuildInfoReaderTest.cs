using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Playtest;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    // build-info.json を読む唯一の実装 RepositoryStateProbe.ReadBuildInfo と、その内部委譲先 BuildInfoJson.Parse を検証する（ADR 0059）
    // Verifies RepositoryStateProbe.ReadBuildInfo, the sole file reader, and its internal delegate BuildInfoJson.Parse (ADR 0059)
    public class BuildInfoReaderTest
    {
        [Test]
        public void 配布ビルドのJSONを読める()
        {
            var json = @"{ ""commit"": ""abc"", ""branch"": ""master"", ""masterDataCommit"": ""def"", ""dirty"": false,
                           ""steamBuildLabel"": ""playtest-20260913-1730"", ""builtAt"": ""2026-09-13T17:30:00+09:00"", ""target"": ""StandaloneWindows64"" }";
            var info = BuildInfoJson.Parse(json);
            Assert.AreEqual("abc", info.Commit);
            Assert.IsNull(info.MasterDataCommit);
            Assert.AreEqual("playtest-20260913-1730", info.SteamBuildLabel);
            Assert.IsFalse(info.Dirty);
        }

        // 実装済みの焼く側（ComposeBuildInfoJson）が出すキーは masterCommit。shared-contracts §1の masterDataCommit ではない（ADR 0059）
        // The baking side (ComposeBuildInfoJson) actually emits masterCommit, not shared-contracts §1's masterDataCommit (ADR 0059)
        [Test]
        public void 実際に焼かれるmasterCommitキーを読める()
        {
            var json = @"{ ""commit"": ""abc"", ""branch"": ""master"", ""masterCommit"": ""def"", ""dirty"": true, ""masterDirty"": true, ""builtAt"": ""2026-09-13T17:30:00Z"" }";
            var info = BuildInfoJson.Parse(json);
            Assert.AreEqual("def", info.MasterDataCommit);
            Assert.IsTrue(info.Dirty);
            Assert.IsTrue(info.MasterDataDirty);
            Assert.IsNull(info.SteamBuildLabel);
            Assert.IsNull(info.Target);
        }

        [Test]
        public void 壊れたJSONはnullを返す()
        {
            Assert.IsNull(BuildInfoJson.Parse("{ not json"));
            Assert.IsNull(BuildInfoJson.Parse(""));
        }

        // Editorでは StreamingAssets/build-info.json が存在しないため、不在時の縮退がそのまま検証できる
        // The Editor has no StreamingAssets/build-info.json, so the "absent" degradation path is exercised as-is
        [Test]
        public void build_info_jsonが無いときはnullを返す()
        {
            Assert.IsNull(RepositoryStateProbe.ReadBuildInfo());
        }

        [Test]
        public void manifestはsteamIdとbuildInfoを持ちEditorではnullで書ける()
        {
            var manifest = new BugReportManifest
            {
                CreatedAt = "2026-09-13T12:00:00Z",
                Description = "説明",
                Kind = PlaytestReportKind.Bug,
                SteamId = "",
                BuildInfo = null,
            };
            var json = JObject.Parse(manifest.ToJson());
            Assert.AreEqual("", (string)json["steamId"]);
            Assert.IsTrue(json.ContainsKey("buildInfo"));
            Assert.AreEqual(JTokenType.Null, json["buildInfo"].Type);
        }
    }
}
