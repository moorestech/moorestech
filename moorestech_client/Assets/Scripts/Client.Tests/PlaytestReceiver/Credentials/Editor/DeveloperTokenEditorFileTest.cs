using System;
using System.IO;
using System.Reflection;
using Client.PlaytestReceiver.Credentials;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Credentials
{
    public class DeveloperTokenEditorFileTest
    {
        private string _directory;
        private string _path;
        private const string Token = "mdt_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        [SetUp]
        public void Initialize()
        {
            _directory = Path.Combine(Path.GetTempPath(), "developer-token-editor-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _path = Path.Combine(_directory, "developer-token.txt");
        }

        [TearDown]
        public void DeleteDirectory() { Directory.Delete(_directory, true); }

        [Test]
        public void 保存と置換と削除をEditor経路で行う()
        {
            Assert.AreEqual("Saved", Invoke("Write", _path, " \t" + Token + "\n"));
            Assert.AreEqual(Token, ((PlaytestDeveloperTokenPresent)PlaytestDeveloperTokenFile.Read(_path)).Token);
            var replacement = "mdt_" + new string('b', 64);
            Assert.AreEqual("Saved", Invoke("Write", _path, replacement));
            Assert.AreEqual(replacement, File.ReadAllText(_path));
            Assert.AreEqual("Deleted", Invoke("Delete", _path));
            Assert.IsInstanceOf<PlaytestDeveloperTokenAbsent>(PlaytestDeveloperTokenFile.Read(_path));
            CollectionAssert.IsEmpty(Directory.GetFiles(_directory));
        }

        [Test]
        public void 不正入力と置換失敗は既存資料を維持する()
        {
            File.WriteAllText(_path, Token);
            Assert.AreEqual("InvalidToken", Invoke("Write", _path, "invalid"));
            Assert.AreEqual(Token, File.ReadAllText(_path));
            // 公開先をディレクトリで塞ぎ、書込み後の公開だけを失敗させる
            // Block publication with a directory so failure happens after the temporary write
            var blocked = Path.Combine(_directory, "blocked");
            Directory.CreateDirectory(blocked);
            File.WriteAllText(Path.Combine(blocked, "original"), Token);
            Assert.AreEqual("SaveFailed", Invoke("Write", blocked, Token));
            Assert.AreEqual(Token, File.ReadAllText(Path.Combine(blocked, "original")));
            CollectionAssert.AreEquivalent(new[] { _path }, Directory.GetFiles(_directory));
        }

        private static string Invoke(string method, params object[] arguments)
        {
            // asmdefから既定Editorアセンブリへの参照は持てないため、実操作を反射で呼ぶ
            // An asmdef cannot reference the predefined Editor assembly; invoke the real operation by reflection
            var type = Type.GetType("DeveloperTokenEditorFile, Assembly-CSharp-Editor", true);
            var result = type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments);
            return result.GetType().GetField("Item1").GetValue(result).ToString();
        }
    }
}
