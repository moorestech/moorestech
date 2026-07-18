using System;
using System.IO;
using System.Security.Cryptography;
using Client.WebUiHost.Common;
using Client.WebUiHost.Static;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi.Static
{
    public class WebUiArtifactValidatorTest
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "webui-artifact-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, true);
        }

        [Test]
        public void Editor生成のmetaファイルは未宣言判定から除外される()
        {
            WriteValidArtifact();

            // Unityが生成する.metaを混在させても検証は成功する
            // Validation still passes with Unity-generated .meta files mixed in
            File.WriteAllText(Path.Combine(_root, "index.html.meta"), "meta");
            File.WriteAllText(Path.Combine(_root, WebUiBuildContract.ManifestFileName + ".meta"), "meta");

            Assert.IsTrue(WebUiArtifactValidator.TryValidate(_root, out var failure), failure);
        }

        [Test]
        public void manifest未宣言の実ファイルは拒否される()
        {
            WriteValidArtifact();
            File.WriteAllText(Path.Combine(_root, "rogue.js"), "x");

            Assert.IsFalse(WebUiArtifactValidator.TryValidate(_root, out var failure));
            Assert.AreEqual("artifact contains files not declared by manifest", failure);
        }

        [Test]
        public void ハッシュ不一致は拒否される()
        {
            WriteValidArtifact();
            File.WriteAllText(Path.Combine(_root, "index.html"), "<html>tampered</html>");

            Assert.IsFalse(WebUiArtifactValidator.TryValidate(_root, out var failure));
            Assert.AreEqual("artifact hash mismatch: index.html", failure);
        }

        // 正規manifest+index.htmlの最小成果物を書き出す
        // Write the minimal valid artifact: canonical manifest + index.html
        private void WriteValidArtifact()
        {
            var indexPath = Path.Combine(_root, "index.html");
            File.WriteAllText(indexPath, "<html></html>");

            var manifest = new WebUiArtifactManifest
            {
                contractVersion = WebUiBuildContract.ContractVersion,
                buildVersion = Application.version,
                files = new[] { new WebUiArtifactFile { path = "index.html", sha256 = Sha256Of(indexPath) } },
            };
            File.WriteAllText(Path.Combine(_root, WebUiBuildContract.ManifestFileName), JsonUtility.ToJson(manifest));
        }

        private static string Sha256Of(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
