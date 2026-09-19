using System.IO;
using System.Linq;
using Client.Game.Localization;
using Client.Tests.Support;
using Core.Master;
using Mooresmaster.LocalizationCsv;
using Mooresmaster.Model.ChallengesModule;
using NUnit.Framework;
using Server.Boot;

namespace Client.Tests.EarlyGame
{
    /// <summary>
    ///     連結ゴーストピンの原文と訳を確認
    ///     Confirms chain-ghost pin wording has source text and a translation
    /// </summary>
    public class EarlyGameChainPinTextTest
    {
        private const string ServerDirectoryName = "server_v8";
        private const string MapDirectoryPath = "server_v8/map";
        private const string MasterDirectoryPath = "server_v8/mods/moorestechAlphaMod_8/master";
        private const string LocalizationCsvPath = "server_v8/mods/moorestechAlphaMod_8/localization/localization.csv";

        private string _extractionRoot;

        [TearDown]
        public void DeleteExtractedMaster()
        {
            if (Directory.Exists(_extractionRoot)) Directory.Delete(_extractionRoot, true);
        }

        [Test]
        public void 連結ゴーストのピン文言は原文と訳の両方を持つ()
        {
            _extractionRoot = PinnedMasterRepository.ExtractPinnedDirectories(MapDirectoryPath, MasterDirectoryPath);
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(Path.Combine(_extractionRoot, ServerDirectoryName)));

            var chainTutorials = MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements
                .SelectMany(category => category.Challenges)
                .SelectMany(challenge => challenge.Tutorials)
                .Where(tutorial => tutorial.TutorialParam is ChainBlockPlacePreviewTutorialParam)
                .ToList();
            Assert.IsNotEmpty(chainTutorials, "the pinned v8 master has no chain preview tutorial; this guard no longer covers anything");

            // ピンはtutorialGuidをキーに文言を引くため、原文と訳行が揃わないと欠落プレースホルダが出る
            // Pins look up wording by tutorialGuid, so a missing source or translation row renders the placeholder
            var sources = MasterSourceTextCollector.Collect();
            var csv = LocalizationCsvParser.Parse(PinnedMasterRepository.ReadPinnedFile(LocalizationCsvPath));
            foreach (var tutorial in chainTutorials)
            {
                var key = $"challengeTutorial.{tutorial.TutorialGuid:D}.text";
                Assert.IsTrue(sources.TryGetValue(key, out var source) && !string.IsNullOrEmpty(source), $"no source text for {key}");

                var row = csv.Rows.FirstOrDefault(r => r.Key == key);
                Assert.IsNotNull(row, $"no translation row for {key}");
                Assert.IsFalse(string.IsNullOrEmpty(row.Source), $"empty source column for {key}");
                for (var i = 0; i < row.Texts.Length; i++)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(row.Texts[i]), $"empty {csv.LanguageCodes[i]} translation for {key}");
                }
            }
        }
    }
}
