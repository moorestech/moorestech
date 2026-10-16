using System;
using System.IO;
using Client.PlaytestReceiver.Credentials;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Credentials
{
    public class PlaytestDeveloperTokenFileTest
    {
        private string _directory;
        private string _path;
        private const string Token = "mdt_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "playtest-token-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _path = Path.Combine(_directory, "nested", "developer-token.txt");
        }

        [TearDown]
        public void TearDown() { Directory.Delete(_directory, true); }

        [Test]
        public void 保存した値を読める()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, Token);
            Assert.AreEqual(Token, ((PlaytestDeveloperTokenPresent)PlaytestDeveloperTokenFile.Read(_path)).Token);
        }

        [Test]
        public void 無ければAbsent() { Assert.IsInstanceOf<PlaytestDeveloperTokenAbsent>(PlaytestDeveloperTokenFile.Read(_path)); }

        [TestCase("hello")]
        [TestCase("mdt_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
        public void 不正な形は理由を返す(string value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, value);
            StringAssert.Contains("形", ((PlaytestDeveloperTokenUnreadable)PlaytestDeveloperTokenFile.Read(_path)).Reason);
        }

        [Test]
        public void 前後の空白と末尾改行を除く()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, " \t" + Token + "\r\n");
            Assert.AreEqual(Token, ((PlaytestDeveloperTokenPresent)PlaytestDeveloperTokenFile.Read(_path)).Token);
        }

        [Test]
        public void 削除後はAbsent()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, Token);
            File.Delete(_path);
            Assert.IsInstanceOf<PlaytestDeveloperTokenAbsent>(PlaytestDeveloperTokenFile.Read(_path));
        }

        [Test]
        public void ディレクトリをファイルとして読めない()
        {
            Assert.IsInstanceOf<PlaytestDeveloperTokenUnreadable>(PlaytestDeveloperTokenFile.Read(_directory));
        }
    }
}
