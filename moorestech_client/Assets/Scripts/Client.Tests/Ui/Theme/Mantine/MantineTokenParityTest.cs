using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Theme.Mantine
{
    /// <summary>
    /// USS が参照する --mantine-* が宣言されていることを確かめる（L2）
    /// Verifies every --mantine-* the USS references is declared (L2)
    /// </summary>
    public class MantineTokenParityTest
    {
        private const string MantineUssPath = "moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/MantineTokens.uss";
        private const string DerivedUssDirectory = "moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/DerivedTokens";
        private const string ClientUiDirectory = "moorestech_client/Assets/Scripts/Client.Ui";
        private static readonly Regex MantineReference = new(@"var\((--mantine-[\w-]+)\)");
        // 宣言は変換器の取り込み対象（mantineTokenNames と部品の寸法）と同じ接頭辞の集合で拾う
        // Declarations are matched with the same prefix set the converter imports (mantineTokenNames and component sizes)
        private static readonly Regex DeclaredName = new(@"^\s*(--(?:mantine|button|input)-[\w-]+)\s*:", RegexOptions.Multiline);

        [Test]
        public void UssMantineReferencesAreDeclared()
        {
            var files = Directory.GetFiles(Path.Combine(RepositoryRoot(), ClientUiDirectory), "*.uss", SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(path) != "MantineTokens.uss" && Path.GetFileName(Path.GetDirectoryName(path)) != "DerivedTokens");
            AssertAllDeclared(files);
        }

        [Test]
        public void MantineNamesAreNotDeclaredInBothFiles()
        {
            // 同名の二重宣言は後勝ちで黙って上書きされる
            // A name declared twice is silently overridden by the later one
            var duplicated = DeclaredNames(MantineUssPath).Intersect(DerivedDeclaredNames()).ToList();
            Assert.IsEmpty(duplicated, $"remove from DerivedTokens/*.uss (MantineTokens.uss already declares it): {string.Join(", ", duplicated)}");
        }

        private static HashSet<string> DeclaredNames(string relativePath)
        {
            return DeclaredName.Matches(File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath))).Select(match => match.Groups[1].Value).ToHashSet();
        }

        // 族ごとに分けた手書きの派生トークンの全ファイルの宣言
        // Declarations across every hand-written derived token file split per family
        private static HashSet<string> DerivedDeclaredNames()
        {
            return Directory.GetFiles(Path.Combine(RepositoryRoot(), DerivedUssDirectory), "*.uss")
                .SelectMany(path => DeclaredName.Matches(File.ReadAllText(path)).Select(match => match.Groups[1].Value)).ToHashSet();
        }

        private static void AssertAllDeclared(IEnumerable<string> files)
        {
            // 未定義の名前を並べる
            // List undeclared names
            var declared = DeclaredNames(MantineUssPath).Union(DerivedDeclaredNames()).ToHashSet();
            var missing = files.SelectMany(path => MantineReference.Matches(File.ReadAllText(path)).Select(match => $"{match.Groups[1].Value} ({Path.GetFileName(path)})"))
                .Where(entry => !declared.Contains(entry.Split(' ')[0])).Distinct().ToList();
            Assert.IsEmpty(missing, $"hand-write it in MantineTokens.uss or DerivedTokens/*.uss: {string.Join(", ", missing)}");
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        }
    }
}
