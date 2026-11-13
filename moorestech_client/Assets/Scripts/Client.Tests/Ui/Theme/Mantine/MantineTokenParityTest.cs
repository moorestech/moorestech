using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Theme.Mantine
{
    /// <summary>
    /// --mantine-*の存在と版一致を確かめる（L2）
    /// Verifies --mantine-* presence and version (L2)
    /// </summary>
    public class MantineTokenParityTest
    {
        private const string MantineUssPath = "moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/MantineTokens.uss";
        private const string DerivedUssDirectory = "moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/DerivedTokens";
        private const string WebSourceDirectory = "moorestech_web/webui/src";
        private const string ClientUiDirectory = "moorestech_client/Assets/Scripts/Client.Ui";
        private const string PnpmLockPath = "moorestech_web/webui/pnpm-lock.yaml";
        private static readonly string[] WebSourcePatterns = { "*.css", "*.ts", "*.tsx" };
        private static readonly Regex MantineReference = new(@"var\((--mantine-[\w-]+)\)");
        // 宣言は変換器の取り込み対象（mantineTokenNames と部品の寸法）と同じ接頭辞の集合で拾う
        // Declarations are matched with the same prefix set the converter imports (mantineTokenNames and component sizes)
        private static readonly Regex DeclaredName = new(@"^\s*(--(?:mantine|button|input)-[\w-]+)\s*:", RegexOptions.Multiline);
        private static readonly Regex HeaderVersion = new(@"@mantine/core (\d+\.\d+\.\d+)");
        private static readonly Regex LockVersion = new(@"'@mantine/core':\s*\n\s*specifier:[^\n]*\n\s*version:\s*(\d+\.\d+\.\d+)");

        [Test]
        public void WebMantineReferencesAreDeclared()
        {
            var files = WebSourcePatterns.SelectMany(pattern => Directory.GetFiles(Path.Combine(RepositoryRoot(), WebSourceDirectory), pattern, SearchOption.AllDirectories));
            AssertAllDeclared(files);
        }

        [Test]
        public void UssMantineReferencesAreDeclared()
        {
            var files = Directory.GetFiles(Path.Combine(RepositoryRoot(), ClientUiDirectory), "*.uss", SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(path) != "MantineTokens.uss" && Path.GetFileName(Path.GetDirectoryName(path)) != "DerivedTokens");
            AssertAllDeclared(files);
        }

        [Test]
        public void GeneratedHeaderMatchesLockedMantineVersion()
        {
            var header = HeaderVersion.Match(File.ReadAllText(Path.Combine(RepositoryRoot(), MantineUssPath)));
            var locked = LockVersion.Match(File.ReadAllText(Path.Combine(RepositoryRoot(), PnpmLockPath)));
            Assert.IsTrue(header.Success, "MantineTokens.uss has no @mantine/core version header");
            Assert.IsTrue(locked.Success, "pnpm-lock.yaml has no @mantine/core entry");
            Assert.AreEqual(locked.Groups[1].Value, header.Groups[1].Value, "regenerate with node tools/uitk/convert-mantine-tokens.mjs");
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
            Assert.IsEmpty(missing, $"add to mantineTokenNames() and regenerate, or hand-write in DerivedTokens/*.uss if translucent: {string.Join(", ", missing)}");
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        }
    }
}
