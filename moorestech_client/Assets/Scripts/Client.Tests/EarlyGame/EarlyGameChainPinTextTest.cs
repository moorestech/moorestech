using System.IO;
using System.Linq;
using Client.Game.Localization;
using Client.Tests.Support;
using Core.Master;
using Mooresmaster.Model.ChallengesModule;
using NUnit.Framework;
using Server.Boot;

namespace Client.Tests.EarlyGame
{
    /// <summary>
    ///     v8マスタの連結ゴーストのピン文言に原文と訳があり、[!key]にならないことを確認
    ///     Proves every chain-ghost pin on the pinned v8 master has source text and a translation row, so no [!key] placeholder shows
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
            var translationCsv = PinnedMasterRepository.ReadPinnedFile(LocalizationCsvPath);
            foreach (var tutorial in chainTutorials)
            {
                var key = $"challengeTutorial.{tutorial.TutorialGuid:D}.text";
                Assert.IsTrue(sources.TryGetValue(key, out var source) && !string.IsNullOrEmpty(source), $"no source text for {key}");
                StringAssert.Contains(key + ",", translationCsv, $"no translation row for {key}");
            }
        }
    }
}
