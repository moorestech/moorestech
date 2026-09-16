using System;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.Playtest;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    // 出所を読む唯一の実装 RepositoryStateProbe.ReadBuildOrigin と、その内部委譲先 BuildInfoJson.Parse を検証する（ADR 0059・F01・F02）
    // Verifies RepositoryStateProbe.ReadBuildOrigin, the sole origin reader, and its internal delegate BuildInfoJson.Parse (ADR 0059, F01, F02)
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
            Assert.AreEqual(false, info.Dirty);
        }

        // 実装済みの焼く側（ComposeBuildInfoJson）が出すキーは masterCommit。shared-contracts §1の masterDataCommit ではない（ADR 0059）
        // The baking side (ComposeBuildInfoJson) actually emits masterCommit, not shared-contracts §1's masterDataCommit (ADR 0059)
        [Test]
        public void 実際に焼かれるmasterCommitキーを読める()
        {
            var json = @"{ ""commit"": ""abc"", ""branch"": ""master"", ""masterCommit"": ""def"", ""dirty"": true, ""masterDirty"": true, ""builtAt"": ""2026-09-13T17:30:00Z"" }";
            var info = BuildInfoJson.Parse(json);
            Assert.AreEqual("def", info.MasterDataCommit);
            Assert.AreEqual(true, info.Dirty);
            Assert.AreEqual(true, info.MasterDataDirty);
            Assert.IsNull(info.SteamBuildLabel);
            Assert.IsNull(info.Target);
        }

        // キーが欠けた焼き込み情報を false や "" で埋めると「クリーンな作業ツリー」という実値に化ける（F02）
        // Filling a missing key with false or "" would pose as a real "clean working tree" (F02)
        [Test]
        public void 欠けた必須キーは既定値で埋めずnullのまま欠損として数える()
        {
            var info = BuildInfoJson.Parse(@"{ ""commit"": """", ""masterCommit"": ""def"" }");

            Assert.IsNull(info.Commit, "空文字のcommitが実値として残っている");
            Assert.IsNull(info.Dirty, "欠けたdirtyがfalse（クリーン）に化けている");
            Assert.IsNull(info.MasterDataDirty, "欠けたmasterDirtyがfalse（クリーン）に化けている");

            var items = BuildOriginReading.Baked(info).Missing.Select(item => item.Item).ToList();
            CollectionAssert.IsSubsetOf(new[] { "buildInfo.commit", "buildInfo.branch", "buildInfo.dirty", "buildInfo.masterDirty" }, items);
        }

        [Test]
        public void 壊れたJSONはnullを返す()
        {
            Assert.IsNull(BuildInfoJson.Parse("{ not json"));
            Assert.IsNull(BuildInfoJson.Parse(""));
        }

        // Editorでは StreamingAssets/build-info.json を読まず、出所は3状態のうち Editor として返る
        // The Editor never reads StreamingAssets/build-info.json, and the origin comes back as the Editor state of the three
        [Test]
        public void Editorでは出所がEditorとして返りbuildInfoはnull()
        {
            Assert.AreEqual(BuildOriginKind.Editor, RepositoryStateProbe.ReadBuildOrigin().Kind);
            Assert.IsNull(RepositoryStateProbe.ReadBuildInfo());
        }

        // 焼き込み情報の無い配布ビルドは git probe へ入らない。入ると配布先の無関係な作業ツリーを名乗る（F01）
        // A distributed build without baked info never enters the git probe; doing so would claim some unrelated working tree on the player's machine (F01)
        [Test]
        public void 焼き込み情報の無いビルドはgit_probeへ入らず出所不明を欠損に残す()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"moorestech-origin-{Guid.NewGuid():N}");
            var origin = BuildOriginReading.WithoutInfo("build-info.json が無い");
            var manifest = BugReportManifest.CreateHeader("説明", PlaytestReportKind.Bug, null, origin);

            BugReportRepositoryFiles.Write(directory, manifest, origin, RepositoryStateProbe.RepositoryRoot, RepositoryStateProbe.MasterDataRoot);

            Assert.IsNull(manifest.Repository, "出所不明なのにリポジトリ状態を名乗っている");
            Assert.IsNull(manifest.MasterData);
            Assert.IsFalse(Directory.Exists(directory), "出所不明なのに作業ツリーの差分を書き出している");
            Assert.IsTrue(manifest.Missing.Any(item => item.Item == "repository" && item.Reason == "build-info.json が無い"));
            Assert.IsTrue(manifest.Missing.Any(item => item.Item == "steamId"), "SteamIDが無い理由がmissingに無い");
        }

        [Test]
        public void manifestはsteamIdとbuildInfoを持ちEditorではnullで書ける()
        {
            var manifest = BugReportManifest.CreateHeader("説明", PlaytestReportKind.Bug, null, BuildOriginReading.Editor());
            var json = JObject.Parse(manifest.ToJson());

            Assert.IsTrue(json.ContainsKey("steamId"));
            Assert.AreEqual(JTokenType.Null, json["steamId"].Type, "取れなかったSteamIDが空文字の実値で出ている");
            Assert.IsTrue(json.ContainsKey("buildInfo"));
            Assert.AreEqual(JTokenType.Null, json["buildInfo"].Type);
        }
    }
}
