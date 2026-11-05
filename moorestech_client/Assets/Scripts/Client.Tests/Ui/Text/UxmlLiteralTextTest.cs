using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Text
{
    /// <summary>
    /// UXML に text= などの生文字を書かせない（キーは LocalizedLabel.SetKey で入れる。noJsxVisibleLiteral の後継）（L2）
    /// Forbids literal text= style attributes in UXML (keys go through LocalizedLabel.SetKey; successor of noJsxVisibleLiteral) (L2)
    /// </summary>
    public class UxmlLiteralTextTest
    {
        private static readonly Regex LiteralAttribute = new("\\b(text|label|tooltip)\\s*=\\s*\"([^\"]+)\"");

        public static IReadOnlyList<string> FindLiteralTextAttributes(string uxml)
        {
            var found = new List<string>();
            foreach (Match match in LiteralAttribute.Matches(uxml)) found.Add(match.Value);
            return found;
        }

        [Test]
        public void LiteralTextAttributeIsFound()
        {
            Assert.AreEqual(1, FindLiteralTextAttributes("<ui:Label text=\"保存\" />").Count);
        }

        [Test]
        public void ElementWithoutTextAttributeIsNotFound()
        {
            Assert.AreEqual(0, FindLiteralTextAttributes("<ui:Label name=\"title\" />").Count);
        }

        [Test]
        public void EmptyTextAttributeIsNotFound()
        {
            Assert.AreEqual(0, FindLiteralTextAttributes("<ui:Label text=\"\" />").Count);
        }

        [Test]
        public void NoUxmlUnderClientUiHasLiteralText()
        {
            var uiRoot = Path.Combine(Application.dataPath, "Scripts/Client.Ui");
            var files = Directory.GetFiles(uiRoot, "*.uxml", SearchOption.AllDirectories);
            var violations = new List<string>();
            foreach (var file in files)
            {
                foreach (var literal in FindLiteralTextAttributes(File.ReadAllText(file))) violations.Add($"{file}: {literal}");
            }

            Assert.IsEmpty(violations, string.Join("\n", violations));
            Debug.Log($"[UxmlLiteralTextTest] scanned {files.Length} uxml file(s) under Client.Ui, 0 literals");
        }
    }
}
