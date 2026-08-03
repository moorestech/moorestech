using System;
using System.Collections.Generic;
using System.IO;
using Client.Localization;
using Core.Master;
using Mod.Loader;
using Mooresmaster.LocalizationCsv;
using NUnit.Framework;

namespace Client.Tests.Localization.Composition
{
    public class ModLocalizationMergerTest
    {
        private string temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                $"mod-localization-{Guid.NewGuid():N}");
            Directory.CreateDirectory(temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true);
        }

        [Test]
        public void LaterModInMasterOrderWinsRegardlessOfResourceDictionaryOrder()
        {
            CreateMod("first-folder", "author:first", "item.test.name,First Source,First English,最初");
            CreateMod("second-folder", "author:second", "item.test.name,Second Source,Second English,後");
            var modsResource = new ModsResource(temporaryDirectory);
            var candidate = CreateCandidate();

            // リソース列挙と逆の明示順で後勝ちを検証する
            // Verify later-wins behavior with an explicit order opposite resource enumeration
            ModLocalizationMerger.Merge(
                modsResource,
                new[] { new ModId("author:second"), new ModId("author:first") },
                candidate);

            Assert.AreEqual("First Source", candidate.SourceTexts["item.test.name"]);
            Assert.AreEqual("First English", candidate.Languages["english"]["item.test.name"]);
            Assert.AreEqual("最初", candidate.Languages["japanese"]["item.test.name"]);
        }

        [Test]
        public void ModWithoutLocalizationCsvIsSkipped()
        {
            CreateMod("without-csv", "author:without", null);
            var modsResource = new ModsResource(temporaryDirectory);
            var candidate = CreateCandidate();

            Assert.DoesNotThrow(() => ModLocalizationMerger.Merge(
                modsResource,
                new[] { new ModId("author:without") },
                candidate));
            Assert.That(candidate.Languages["english"], Is.Empty);
        }

        [Test]
        public void EmptySourceAndTranslationDoNotOverwriteExistingValues()
        {
            var candidate = CreateCandidate();
            candidate.Languages["english"]["item.test.name"] = "Vanilla English";
            candidate.Languages["japanese"]["item.test.name"] = "既存日本語";
            candidate.SourceTexts["item.test.name"] = "既存原文";
            var csv = "key,Source,english,japanese\nitem.test.name,,Mod English,\n";

            ModLocalizationMerger.MergeCsv(csv, candidate);

            Assert.AreEqual("既存日本語", candidate.Languages["japanese"]["item.test.name"]);
            Assert.AreEqual("Mod English", candidate.Languages["english"]["item.test.name"]);
            Assert.AreEqual("既存原文", candidate.SourceTexts["item.test.name"]);
        }

        [Test]
        public void UnknownLanguageColumnIsRejectedWithLocalizationCsvError()
        {
            var candidate = CreateCandidate();
            var csv = "key,Source,klingon\nitem.test.name,Source,Qapla\n";

            var exception = Assert.Throws<LocalizationCsvException>(
                () => ModLocalizationMerger.MergeCsv(csv, candidate));

            StringAssert.Contains("klingon", exception.Message);
        }

        private void CreateMod(string directoryName, string fullModId, string localizationRow)
        {
            var separatorIndex = fullModId.IndexOf(':');
            var author = fullModId.Substring(0, separatorIndex);
            var id = fullModId.Substring(separatorIndex + 1);
            var modDirectory = Path.Combine(temporaryDirectory, directoryName);
            var masterDirectory = Path.Combine(modDirectory, "master");
            Directory.CreateDirectory(masterDirectory);
            File.WriteAllText(
                Path.Combine(masterDirectory, "modMeta.json"),
                $"{{\"id\":\"{id}\",\"name\":\"{id}\",\"version\":\"1.0\",\"author\":\"{author}\",\"description\":\"test\"}}");
            if (localizationRow == null) return;

            var localizationDirectory = Path.Combine(modDirectory, "localization");
            Directory.CreateDirectory(localizationDirectory);
            File.WriteAllText(
                Path.Combine(localizationDirectory, "localization.csv"),
                $"key,Source,english,japanese\n{localizationRow}\n");
        }

        private static LocalizationDictionaryCandidate CreateCandidate()
        {
            return new LocalizationDictionaryCandidate(
                new Dictionary<string, Dictionary<string, string>>
                {
                    { "english", new Dictionary<string, string>() },
                    { "japanese", new Dictionary<string, string>() },
                },
                new Dictionary<string, string>());
        }
    }
}
