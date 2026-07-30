using System;
using System.Globalization;
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
            // Guid導出キーの名前空間とフィールド名を固定する
            // Pin the namespaces and field names of Guid-derived keys
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

        [Test]
        public void SkitBuildersPreserveCommandForgeFieldCasing()
        {
            // CommandForgeフィールド名の大文字小文字をそのまま維持する
            // Preserve the exact casing of CommandForge schema fields
            Assert.AreEqual("skit.opening.42.body", ContentLocalizationKeys.SkitTextBody("opening", 42));
            Assert.AreEqual("skit.opening.42.body", ContentLocalizationKeys.SkitBackgroundBody("opening", 42));
            Assert.AreEqual("skit.opening.42.Option1Tag", ContentLocalizationKeys.SkitSelectionOption1Tag("opening", 42));
            Assert.AreEqual("skit.opening.42.Option2Tag", ContentLocalizationKeys.SkitSelectionOption2Tag("opening", 42));
            Assert.AreEqual("skit.opening.42.Option3Tag", ContentLocalizationKeys.SkitSelectionOption3Tag("opening", 42));
            Assert.AreEqual("skit.opening.42.overrideCharacterName", ContentLocalizationKeys.SkitOverrideCharacterName("opening", 42));
        }

        [Test]
        [SetCulture("en-US")]
        public void SkitCommandIdUsesInvariantFormatting()
        {
            var customCulture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            customCulture.NumberFormat.NegativeSign = "~";
            CultureInfo.CurrentCulture = customCulture;

            // 負号を変えたカルチャでも不変形式のマイナス記号を維持する
            // Preserve the invariant minus sign under a culture with a mutated negative sign
            Assert.AreEqual("skit.導入.-123.body", ContentLocalizationKeys.SkitTextBody("導入", -123));
        }
    }
}
