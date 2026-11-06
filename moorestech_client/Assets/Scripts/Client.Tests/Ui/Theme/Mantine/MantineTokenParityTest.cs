using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Theme.Mantine
{
    /// <summary>
    /// Web と UITK が参照する --mantine-* がすべて MantineTokens.uss か DerivedTokens.uss にあり、生成元の Mantine の版が lockfile と同じことを確かめる（L2・設計書 C4）
    /// Verifies every --mantine-* referenced by the Web and UITK is declared in MantineTokens.uss or DerivedTokens.uss and the generated version matches the lockfile (L2, design C4)
    /// </summary>
    public class MantineTokenParityTest
    {
        private const string MantineUssPath = "moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/MantineTokens.uss";
        private const string DerivedUssPath = "moorestech_client/Assets/Scripts/Client.Ui/Foundation/Theme/DerivedTokens.uss";
        private const string WebSourceDirectory = "moorestech_web/webui/src";
        private const string ClientUiDirectory = "moorestech_client/Assets/Scripts/Client.Ui";
        private const string PnpmLockPath = "moorestech_web/webui/pnpm-lock.yaml";
        private static readonly string[] WebSourcePatterns = { "*.css", "*.ts", "*.tsx" };
        private static readonly Regex MantineReference = new(@"var\((--mantine-[\w-]+)\)");
        private static readonly Regex DeclaredName = new(@"^\s*(--mantine-[\w-]+)\s*:", RegexOptions.Multiline);
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
                .Where(path => Path.GetFileName(path) != "MantineTokens.uss" && Path.GetFileName(path) != "DerivedTokens.uss");
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
            var duplicated = DeclaredNames(MantineUssPath).Intersect(DeclaredNames(DerivedUssPath)).ToList();
            Assert.IsEmpty(duplicated, $"remove from DerivedTokens.uss (MantineTokens.uss already declares it): {string.Join(", ", duplicated)}");
        }

        private static HashSet<string> DeclaredNames(string relativePath)
        {
            return DeclaredName.Matches(File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath))).Select(match => match.Groups[1].Value).ToHashSet();
        }

        private static void AssertAllDeclared(IEnumerable<string> files)
        {
            // 参照している名前のうち、生成物にも手書きの前計算にも無いものを並べる
            // List referenced names declared in neither the generated file nor the hand-written derived tokens
            var declared = DeclaredNames(MantineUssPath).Union(DeclaredNames(DerivedUssPath)).ToHashSet();
            var missing = files.SelectMany(path => MantineReference.Matches(File.ReadAllText(path)).Select(match => $"{match.Groups[1].Value} ({Path.GetFileName(path)})"))
                .Where(entry => !declared.Contains(entry.Split(' ')[0])).Distinct().ToList();
            Assert.IsEmpty(missing, $"add to mantineTokenNames() and regenerate, or hand-write in DerivedTokens.uss if translucent: {string.Join(", ", missing)}");
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        }
    }
}
