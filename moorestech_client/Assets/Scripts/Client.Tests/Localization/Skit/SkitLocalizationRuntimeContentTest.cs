using System;
using System.Diagnostics;
using System.IO;
using Mooresmaster.LocalizationCsv;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Localization.Skit
{
    public class SkitLocalizationRuntimeContentTest
    {
        private static readonly string[] QaSentinels =
        {
            "MOD ENGLISH",
            "MOD JAPANESE",
            "SKIT ENGLISH",
            "SKIT JAPANESE",
        };

        [TestCase("english")]
        [TestCase("japanese")]
        public void AddressableSkitRuntimeValuesExcludeQaSentinels(string languageCode)
        {
            var path = Path.Combine(
                Application.dataPath,
                "AddressableResources",
                "Skit",
                "i18n",
                languageCode + ".json");
            var translations = (JObject)JObject.Parse(File.ReadAllText(path))["translations"];

            // 実行時に解決されるskit値だけをQA用識別子から保護する
            // Protect only runtime-resolved skit values from QA identifiers
            foreach (var property in translations.Properties())
            {
                if (!property.Name.StartsWith("skit.", StringComparison.Ordinal)) continue;
                AssertRuntimeValue(property.Name, (string)property.Value);
            }
        }

        [Test]
        public void PinnedModSkitRuntimeValuesExcludeQaSentinels()
        {
            var repositoryRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", ".."));
            var revisionJson = RunGit(
                repositoryRoot,
                "show HEAD:.moorestech-external-revisions.json");
            var masterRevision = FindMasterRevision(JObject.Parse(revisionJson));

            // 共通gitディレクトリから本体repoを特定し、コミット済みpinを直接読む
            // Locate the primary repo via the common git directory and read the committed pin directly
            var commonGitDirectory = RunGit(
                repositoryRoot,
                "rev-parse --path-format=absolute --git-common-dir").Trim();
            var primaryRepositoryRoot = Directory.GetParent(commonGitDirectory).FullName;
            var masterRepositoryRoot = Path.GetFullPath(Path.Combine(
                primaryRepositoryRoot,
                (string)masterRevision["relativePath"]));
            Assert.IsTrue(
                Directory.Exists(masterRepositoryRoot),
                $"Pinned master repository not found: {masterRepositoryRoot}");

            // pin先commitの本番CSVを本番parserへ通して全runtime列を検査する
            // Parse the production CSV at the pinned commit and inspect every runtime column
            var csvText = RunGit(
                masterRepositoryRoot,
                $"show {(string)masterRevision["commitHash"]}:server_v8/mods/moorestechAlphaMod_8/localization/localization.csv");
            var csv = LocalizationCsvParser.Parse(csvText);
            foreach (var row in csv.Rows)
            {
                if (!row.Key.StartsWith("skit.", StringComparison.Ordinal)) continue;
                AssertRuntimeValue($"{row.Key}:Source", row.Source);
                for (var index = 0; index < row.Texts.Length; index++)
                {
                    AssertRuntimeValue($"{row.Key}:{csv.LanguageCodes[index]}", row.Texts[index]);
                }
            }
        }

        private static JObject FindMasterRevision(JObject revisionRoot)
        {
            foreach (var token in (JArray)revisionRoot["repositories"])
            {
                var revision = (JObject)token;
                if ((string)revision["key"] == "moorestech_master") return revision;
            }

            Assert.Fail("Committed external revisions do not contain moorestech_master");
            return null;
        }

        private static void AssertRuntimeValue(string location, string value)
        {
            foreach (var sentinel in QaSentinels)
            {
                StringAssert.DoesNotContain(sentinel, value, location);
            }
        }

        private static string RunGit(string workingDirectory, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            // 外部git境界の終了コードと標準エラーをテスト失敗へ変換する
            // Convert the external git boundary's exit code and stderr into a test failure
            using var process = Process.Start(startInfo);
            Assert.IsNotNull(process, $"Failed to start git in {workingDirectory}");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.AreEqual(0, process.ExitCode, standardError);
            return standardOutput;
        }
    }
}
