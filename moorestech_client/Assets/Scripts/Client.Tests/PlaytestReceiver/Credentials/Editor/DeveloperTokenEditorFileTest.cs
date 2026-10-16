using System;
using System.IO;
using System.Diagnostics;
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
        }

        [Test]
        public void 同じ保存先の書込み失敗でも旧トークンを維持する()
        {
            if (Path.DirectorySeparatorChar == '\\') Assert.Ignore("Unixディレクトリ権限の障害注入。Windowsの同等検証は未実施");
            if (Environment.UserName == "root") Assert.Ignore("root実行ではchmodで権限拒否を再現できないためスキップ（非rootの開発機で検証される）");
            File.WriteAllText(_path, Token);
            var replacement = "mdt_" + new string('b', 64);
            SetDirectoryMode("500");
            try
            {
                // 直接上書きは可能だが一時ファイルを作れない同じ保存先で保証を検証する
                // The same destination permits direct overwrite but rejects staging a temporary file
                File.WriteAllText(_path, replacement);
                File.WriteAllText(_path, Token);
                Assert.AreEqual("SaveFailed", Invoke("Write", _path, replacement));
                Assert.AreEqual(Token, ((PlaytestDeveloperTokenPresent)PlaytestDeveloperTokenFile.Read(_path)).Token);
                CollectionAssert.AreEquivalent(new[] { _path }, Directory.GetFiles(_directory));
            }
            finally { SetDirectoryMode("700"); }

            #region Internal

            void SetDirectoryMode(string mode)
            {
                using var process = Process.Start(new ProcessStartInfo("/bin/chmod", mode + " \"" + _directory + "\"") { UseShellExecute = false });
                process.WaitForExit();
                Assert.AreEqual(0, process.ExitCode);
            }

            #endregion
        }

        [Test]
        public void Unixの保存先と置換後ファイルは所有者限定になる()
        {
            if (UnityEngine.Application.platform != UnityEngine.RuntimePlatform.OSXEditor) Assert.Ignore("macOS statの実測。Windows ACLとLinux modeの検証は未実施");
            Assert.AreEqual("Saved", Invoke("Write", _path, Token));
            AssertMode(_directory, "700");
            AssertMode(_path, "600");
            Assert.AreEqual("Saved", Invoke("Write", _path, "mdt_" + new string('c', 64)));
            AssertMode(_path, "600");

            #region Internal

            void AssertMode(string path, string expected)
            {
                using var process = Process.Start(new ProcessStartInfo("/usr/bin/stat", "-f %OLp \"" + path + "\"") { UseShellExecute = false, RedirectStandardOutput = true });
                var actual = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                Assert.AreEqual(0, process.ExitCode);
                Assert.AreEqual(expected, actual);
            }

            #endregion
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
