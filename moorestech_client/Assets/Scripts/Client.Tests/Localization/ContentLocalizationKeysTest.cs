using System;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Localization
{
    public class ContentLocalizationKeysTest
    {
        private const string GuidSegment = "01234567-89ab-cdef-0123-456789abcdef";
        private static readonly Guid ContentGuid = Guid.Parse(GuidSegment);

        [Test]
        public void 宣言表の全行が小文字D形式Guidの導出キーを生成する()
        {
            // 宣言表 Localization/content_keys.csv の全行を固定
            // Pin every row of the Localization/content_keys.csv declaration table
            Assert.AreEqual($"item.{GuidSegment}.name", ContentLocalizationKeys.ItemName(ContentGuid).Key);
            Assert.AreEqual($"block.{GuidSegment}.name", ContentLocalizationKeys.BlockName(ContentGuid).Key);
            Assert.AreEqual($"research.{GuidSegment}.name", ContentLocalizationKeys.ResearchName(ContentGuid).Key);
            Assert.AreEqual($"research.{GuidSegment}.description", ContentLocalizationKeys.ResearchDescription(ContentGuid).Key);
            Assert.AreEqual($"challenge.{GuidSegment}.title", ContentLocalizationKeys.ChallengeTitle(ContentGuid).Key);
            Assert.AreEqual($"challenge.{GuidSegment}.summary", ContentLocalizationKeys.ChallengeSummary(ContentGuid).Key);
            Assert.AreEqual($"challengeCategory.{GuidSegment}.name", ContentLocalizationKeys.ChallengeCategoryName(ContentGuid).Key);
            Assert.AreEqual($"challengeCategory.{GuidSegment}.description", ContentLocalizationKeys.ChallengeCategoryDescription(ContentGuid).Key);
            Assert.AreEqual($"character.{GuidSegment}.name", ContentLocalizationKeys.CharacterName(ContentGuid).Key);
            Assert.AreEqual($"buildMenuCategory.{GuidSegment}.name", ContentLocalizationKeys.BuildMenuCategoryName(ContentGuid).Key);
            Assert.AreEqual($"buildMenuSubCategory.{GuidSegment}.name", ContentLocalizationKeys.BuildMenuSubCategoryName(ContentGuid).Key);
            Assert.AreEqual($"challengeTutorial.{GuidSegment}.text", ContentLocalizationKeys.ChallengeTutorialText(ContentGuid).Key);
            Assert.AreEqual($"connectTool.{GuidSegment}.name", ContentLocalizationKeys.ConnectToolName(ContentGuid).Key);
            Assert.AreEqual($"fluid.{GuidSegment}.name", ContentLocalizationKeys.FluidName(ContentGuid).Key);
        }

        [Test]
        public void 大文字Guid入力でも小文字segmentへ正準化する()
        {
            var upperCaseGuid = Guid.Parse("01234567-89AB-CDEF-0123-456789ABCDEF");

            Assert.AreEqual($"item.{GuidSegment}.name", ContentLocalizationKeys.ItemName(upperCaseGuid).Key);
        }
    }
}
