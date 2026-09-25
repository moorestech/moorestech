using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.BuildOrigin;
using NUnit.Framework;

namespace Client.Tests.BugReport.BuildOrigin
{
    // strictビルドのピン照合元がコミット済みのピンであることを、実際のgitリポジトリで固定する
    // Pins down, with a real git repository, that the strict build compares against the committed pin
    public class MasterDataPinnedCommitTest
    {
        private const string CommittedMasterCommit = "2222222222222222222222222222222222222222";
        private const string RewrittenMasterCommit = "3333333333333333333333333333333333333333";
        private const string RepoCommit = "1111111111111111111111111111111111111111";
        private const string PinFileName = ".moorestech-external-revisions.json";

        private readonly List<string> _temporaryDirectories = new();

        [Test]
        public void コミット済みのピンからmasterDataのコミットを読める()
        {
            var checkoutRoot = CreateCheckoutWithCommittedPin(CommittedMasterCommit);

            Assert.AreEqual(CommittedMasterCommit, MasterDataRootLocator.ReadPinnedCommit(checkoutRoot, out var unreadableReason));
            Assert.IsNull(unreadableReason, unreadableReason);
        }

        // GUIビルドの同期が作業ツリーのピンを実HEADへ書き戻しても、照合元はコミット済みの値のまま
        // Even after the GUI build sync rewrites the working-tree pin to the actual HEAD, the comparison source stays the committed value
        [Test]
        public void 作業ツリーのピンが書き戻されてもコミット済みの値で照合しstrictが失敗する()
        {
            var checkoutRoot = CreateCheckoutWithCommittedPin(CommittedMasterCommit);
            WritePin(checkoutRoot, RewrittenMasterCommit);

            var pinned = MasterDataRootLocator.ReadPinnedCommit(checkoutRoot, out var unreadableReason);
            Assert.AreEqual(CommittedMasterCommit, pinned);

            var masterAtRewrittenHead = new RepositoryProbeResult { State = new RepositoryState { Commit = RewrittenMasterCommit, Branch = "HEAD", Dirty = false } };
            var repo = new RepositoryProbeResult { State = new RepositoryState { Commit = RepoCommit, Branch = "master", Dirty = true } };
            BuildInfoComposer.Compose(repo, masterAtRewrittenHead, pinned, unreadableReason, null, null, DateTime.UtcNow, "StandaloneWindows64", true, out var failureReason);
            StringAssert.Contains(CommittedMasterCommit, failureReason);
            StringAssert.Contains(RewrittenMasterCommit, failureReason);
        }

        // 作業ツリーにだけあるピン（未コミット）は照合元にしない。理由にはどのrepoを読んだかが載る
        // An uncommitted, working-tree-only pin is never the source; the reason names the repository that was read
        [Test]
        public void ピンがコミットされていなければ理由を返しstrictの失敗理由に載る()
        {
            var checkoutRoot = CreateTemporaryDirectory();
            WritePin(checkoutRoot, CommittedMasterCommit);

            Assert.IsNull(MasterDataRootLocator.ReadPinnedCommit(checkoutRoot, out var unreadableReason));
            var probe = new RepositoryProbeResult { State = new RepositoryState { Commit = CommittedMasterCommit, Branch = "HEAD", Dirty = false } };
            BuildInfoComposer.Compose(probe, probe, null, unreadableReason, null, null, DateTime.UtcNow, "StandaloneWindows64", true, out var failureReason);
            StringAssert.Contains(checkoutRoot, failureReason);
        }

        // worktree からのビルドでも同梱元はビルドする checkout の隣。relativePath もコミット済みのピンから読む（D-6）
        // Even in a worktree build the bundled master repo sits beside the building checkout; relativePath is read from the committed pin too (D-6)
        [Test]
        public void ビルド用のマスタrepoはビルドするcheckoutのコミット済みピンのrelativePathで解決する()
        {
            var checkoutRoot = CreateCheckoutWithCommittedPin(CommittedMasterCommit);

            var expected = Path.GetFullPath(Path.Combine(checkoutRoot, "..", "fixture_master"));
            Assert.AreEqual(expected, MasterDataRootLocator.ResolveForBuildingCheckout(checkoutRoot));
        }

        [TearDown]
        public void DeleteTemporaryDirectories()
        {
            foreach (var directory in _temporaryDirectories)
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
            _temporaryDirectories.Clear();
        }

        private string CreateTemporaryDirectory()
        {
            var parent = Path.Combine(Path.GetTempPath(), "moores-pinned-commit-" + Guid.NewGuid().ToString("N"));
            _temporaryDirectories.Add(parent);
            var checkoutRoot = Path.Combine(parent, "checkout");
            Directory.CreateDirectory(checkoutRoot);
            return checkoutRoot;
        }

        private string CreateCheckoutWithCommittedPin(string commit)
        {
            var checkoutRoot = CreateTemporaryDirectory();
            WritePin(checkoutRoot, commit);

            // 利用者のglobal設定（署名・フック）に左右されないよう、コミットに必要な設定をその場で渡す
            // Pass the settings a commit needs inline so the user's global config (signing, hooks) cannot interfere
            RunGit(checkoutRoot, "init -q");
            RunGit(checkoutRoot, $"add {PinFileName}");
            RunGit(checkoutRoot, "-c user.name=moores-test -c user.email=moores-test@example.com -c commit.gpgsign=false commit -q --no-verify -m pin");
            return checkoutRoot;
        }

        private static void WritePin(string checkoutRoot, string commit)
        {
            File.WriteAllText(Path.Combine(checkoutRoot, PinFileName),
                "{\"repositories\":[{\"key\":\"moorestech_master\",\"relativePath\":\"../fixture_master\",\"commitHash\":\"" + commit + "\"}]}");
        }

        private static void RunGit(string workingDirectory, string arguments)
        {
            Assert.IsTrue(RepositoryStateProbe.TryGit(workingDirectory, arguments, out _, out var error), error);
        }
    }
}
