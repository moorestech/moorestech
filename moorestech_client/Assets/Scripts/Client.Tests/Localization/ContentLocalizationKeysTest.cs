using System;
using Client.Localization;
using NUnit.Framework;

namespace Client.Tests.Localization
{
    public class ContentLocalizationKeysTest
    {
        private static readonly Guid ContentGuid = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

        [Test]
        public void GuidBuildersUseCanonicalLowercaseSegments()
        {
            // 導出キーの名前空間とfield固定
            // Pin derived-key namespaces and fields
            Assert.AreEqual("item.01234567-89ab-cdef-0123-456789abcdef.name", ContentLocalizationKeys.ItemName(ContentGuid));
            Assert.AreEqual("block.01234567-89ab-cdef-0123-456789abcdef.name", ContentLocalizationKeys.BlockName(ContentGuid));
            Assert.AreEqual("research.01234567-89ab-cdef-0123-456789abcdef.name", ContentLocalizationKeys.ResearchNodeName(ContentGuid));
            Assert.AreEqual("research.01234567-89ab-cdef-0123-456789abcdef.description", ContentLocalizationKeys.ResearchNodeDescription(ContentGuid));
            Assert.AreEqual("challenge.01234567-89ab-cdef-0123-456789abcdef.title", ContentLocalizationKeys.ChallengeTitle(ContentGuid));
            Assert.AreEqual("challenge.01234567-89ab-cdef-0123-456789abcdef.summary", ContentLocalizationKeys.ChallengeSummary(ContentGuid));
            Assert.AreEqual("challengeCategory.01234567-89ab-cdef-0123-456789abcdef.name", ContentLocalizationKeys.ChallengeCategoryName(ContentGuid));
            Assert.AreEqual("character.01234567-89ab-cdef-0123-456789abcdef.name", ContentLocalizationKeys.CharacterName(ContentGuid));
            Assert.AreEqual("buildMenuCategory.01234567-89ab-cdef-0123-456789abcdef.name", ContentLocalizationKeys.BuildMenuCategoryName(ContentGuid));
            Assert.AreEqual("buildMenuSubCategory.01234567-89ab-cdef-0123-456789abcdef.name", ContentLocalizationKeys.BuildMenuSubCategoryName(ContentGuid));
        }

        [Test]
        public void ChallengeCategoryDescriptionBuilderUsesCanonicalLowercaseSegment()
        {
            Assert.AreEqual(
                "challengeCategory.01234567-89ab-cdef-0123-456789abcdef.description",
                ContentLocalizationKeys.ChallengeCategoryDescription(ContentGuid));
        }

    }
}
