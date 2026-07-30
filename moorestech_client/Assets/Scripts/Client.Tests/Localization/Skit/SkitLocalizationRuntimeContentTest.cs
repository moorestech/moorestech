using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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

            // 実行時skit値からQA識別子を排除
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

            // 共通gitから本体repoとpinを特定
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

            // pin先CSVを本番parserで検査
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

            // 外部gitを有界時間で検証
            // Validate the external git process within bounded time
            using var process = Process.Start(startInfo);
            Assert.IsNotNull(process, $"Failed to start git in {workingDirectory}");
            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(5000))
            {
                process.Kill();
                Assert.Fail($"git timed out in {workingDirectory}");
            }

            var outputTasks = new Task[] { standardOutputTask, standardErrorTask };
            Assert.IsTrue(Task.WaitAll(outputTasks, 2000), "git output streams did not close");
            Assert.AreEqual(0, process.ExitCode, standardErrorTask.Result);
            return standardOutputTask.Result;
        }
    }
}
