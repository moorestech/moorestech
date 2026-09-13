using System.IO;
using Client.Game.InGame.BugReport;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class RepositoryStateProbeTest
    {
        [Test]
        public void 本repoのHEADとブランチが取れる()
        {
            var result = RepositoryStateProbe.ProbeGit(RepositoryStateProbe.RepositoryRoot);
            Assert.IsNull(result.Error, result.Error);
            Assert.AreEqual(40, result.State.Commit.Length);
            Assert.IsNotEmpty(result.State.Branch);
        }

        [Test]
        public void 存在しないパスはErrorに理由が入る()
        {
            var result = RepositoryStateProbe.ProbeGit("/nonexistent/path/for/test");
            Assert.IsNotNull(result.Error);
            Assert.IsNull(result.State);
        }

        // gitリポジトリでないディレクトリでも例外を投げず、理由だけを返して呼び出し側を進ませる
        // A non-git directory must not throw either; it returns a reason so the caller can carry on
        [Test]
        public void gitリポジトリでないディレクトリはErrorに理由が入る()
        {
            var result = RepositoryStateProbe.ProbeGit(Path.GetTempPath());
            Assert.IsNotNull(result.Error);
        }
    }
}
