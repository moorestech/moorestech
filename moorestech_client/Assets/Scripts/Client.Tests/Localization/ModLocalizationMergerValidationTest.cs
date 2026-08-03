using System;
using System.Collections.Generic;
using System.IO;
using Client.Localization;
using Core.Master;
using Mod.Loader;
using Mooresmaster.LocalizationCsv;
using NUnit.Framework;

namespace Client.Tests.Localization
{
    public class ModLocalizationMergerValidationTest
    {
        private string temporaryRoot;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(Path.GetTempPath(), $"mod-merger-validation-{Guid.NewGuid():N}");
            Directory.CreateDirectory(temporaryRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
        }

        [Test]
        public void DuplicateLanguageColumnIsRejectedBeforeDictionaryMutation()
        {
            AssertCsvRejectedWithoutMutation(
                "key,Source,english,english\ncontent.test.name,Changed,Changed,Changed\n",
                "english");
        }

        [Test]
        public void ReservedSourceLanguageColumnIsRejectedBeforeDictionaryMutation()
        {
            AssertCsvRejectedWithoutMutation(
                "key,Source,source\ncontent.test.name,Changed,Changed\n",
                "source");
        }

        [Test]
        public void CsvWithoutEnglishMergesKnownLanguageSubset()
        {
            var candidate = CreateCandidate();
            var csv = "key,Source,japanese\ncontent.subset.name,Subset Source,部分訳\n";

            ModLocalizationMerger.MergeCsv(csv, candidate);

            Assert.AreEqual("部分訳", candidate.Languages["japanese"]["content.subset.name"]);
            Assert.AreEqual("Subset Source", candidate.SourceTexts["content.subset.name"]);
            Assert.IsFalse(candidate.Languages["english"].ContainsKey("content.subset.name"));
        }

        [Test]
        public void DuplicateOrderedModIdIsRejectedBeforeDictionaryMutation()
        {
            var modsResource = CreateResource();
            var candidate = CreateCandidate();

            Assert.Throws<InvalidOperationException>(() => ModLocalizationMerger.Merge(
                modsResource,
                new[] { new ModId("author:single"), new ModId("author:single") },
                candidate));
            Assert.AreEqual("Existing", candidate.Languages["english"]["content.test.name"]);
        }

        [Test]
        public void LoadedModMissingFromOrderIsRejectedBeforeDictionaryMutation()
        {
            var modsResource = CreateResource();
            var candidate = CreateCandidate();

            Assert.Throws<InvalidOperationException>(() => ModLocalizationMerger.Merge(
                modsResource,
                Array.Empty<ModId>(),
                candidate));
            Assert.AreEqual("Existing", candidate.Languages["english"]["content.test.name"]);
        }

        private void AssertCsvRejectedWithoutMutation(string csv, string expectedMessagePart)
        {
            var candidate = CreateCandidate();

            var exception = Assert.Throws<LocalizationCsvException>(
                () => ModLocalizationMerger.MergeCsv(csv, candidate));

            StringAssert.Contains(expectedMessagePart, exception.Message);
            Assert.AreEqual("Existing", candidate.Languages["english"]["content.test.name"]);
            Assert.AreEqual("既存", candidate.Languages["japanese"]["content.test.name"]);
            Assert.AreEqual("Original", candidate.SourceTexts["content.test.name"]);
        }

        private ModsResource CreateResource()
        {
            var modDirectory = Path.Combine(temporaryRoot, "mod");
            var masterDirectory = Path.Combine(modDirectory, "master");
            Directory.CreateDirectory(masterDirectory);
            File.WriteAllText(
                Path.Combine(masterDirectory, "modMeta.json"),
                "{\"id\":\"single\",\"name\":\"single\",\"version\":\"1.0\",\"author\":\"author\",\"description\":\"test\"}");
            return new ModsResource(temporaryRoot);
        }

        private static LocalizationDictionaryCandidate CreateCandidate()
        {
            return new LocalizationDictionaryCandidate(
                new Dictionary<string, Dictionary<string, string>>
                {
                    { "english", new Dictionary<string, string> { { "content.test.name", "Existing" } } },
                    { "japanese", new Dictionary<string, string> { { "content.test.name", "既存" } } },
                },
                new Dictionary<string, string> { { "content.test.name", "Original" } });
        }
    }
}
