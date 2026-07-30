using System;
using System.Globalization;

namespace Client.Localization
{
    public static class ContentLocalizationKeys
    {
        public static string ItemName(Guid itemGuid)
        {
            return $"item.{itemGuid:D}.name";
        }

        public static string BlockName(Guid blockGuid)
        {
            return $"block.{blockGuid:D}.name";
        }

        public static string ResearchNodeName(Guid researchNodeGuid)
        {
            return $"research.{researchNodeGuid:D}.name";
        }

        public static string ResearchNodeDescription(Guid researchNodeGuid)
        {
            return $"research.{researchNodeGuid:D}.description";
        }

        public static string ChallengeTitle(Guid challengeGuid)
        {
            return $"challenge.{challengeGuid:D}.title";
        }

        public static string ChallengeSummary(Guid challengeGuid)
        {
            return $"challenge.{challengeGuid:D}.summary";
        }

        public static string ChallengeCategoryName(Guid challengeCategoryGuid)
        {
            return $"challengeCategory.{challengeCategoryGuid:D}.name";
        }

        public static string CharacterName(Guid characterGuid)
        {
            return $"character.{characterGuid:D}.name";
        }

        public static string BuildMenuCategoryName(Guid categoryGuid)
        {
            return $"buildMenuCategory.{categoryGuid:D}.name";
        }

        public static string BuildMenuSubCategoryName(Guid subCategoryGuid)
        {
            return $"buildMenuSubCategory.{subCategoryGuid:D}.name";
        }

        public static string SkitTextBody(string skitTitle, int commandId)
        {
            return SkitField(skitTitle, commandId, "body");
        }

        public static string SkitBackgroundBody(string skitTitle, int commandId)
        {
            return SkitField(skitTitle, commandId, "body");
        }

        public static string SkitSelectionOption1Tag(string skitTitle, int commandId)
        {
            return SkitField(skitTitle, commandId, "Option1Tag");
        }

        public static string SkitSelectionOption2Tag(string skitTitle, int commandId)
        {
            return SkitField(skitTitle, commandId, "Option2Tag");
        }

        public static string SkitSelectionOption3Tag(string skitTitle, int commandId)
        {
            return SkitField(skitTitle, commandId, "Option3Tag");
        }

        public static string SkitOverrideCharacterName(string skitTitle, int commandId)
        {
            return SkitField(skitTitle, commandId, "overrideCharacterName");
        }

        private static string SkitField(string skitTitle, int commandId, string field)
        {
            return $"skit.{skitTitle}.{commandId.ToString(CultureInfo.InvariantCulture)}.{field}";
        }
    }
}
