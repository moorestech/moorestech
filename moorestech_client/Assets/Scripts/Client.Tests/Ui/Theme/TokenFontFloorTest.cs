using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Theme
{
    /// <summary>
    /// 生成トークンが文字寸法の下限を持たない（L2）
    /// Generated tokens carry no font-size floor (L2)
    /// </summary>
    public class TokenFontFloorTest
    {
        private const string UiScriptDirectory = "moorestech_client/Assets/Scripts/Client.Ui";

        [Test]
        public void GeneratedTokensCarryNoFontFloor()
        {
            // 文字寸法はWebと同じpx
            // Text sizes match the Web px
            foreach (var path in UssFiles()) StringAssert.DoesNotContain("下限", File.ReadAllText(path), Path.GetFileName(path));
        }

        private static IEnumerable<string> UssFiles()
        {
            return Directory.GetFiles(Path.Combine(RepositoryRoot(), UiScriptDirectory), "*.uss", SearchOption.AllDirectories);
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        }
    }
}
