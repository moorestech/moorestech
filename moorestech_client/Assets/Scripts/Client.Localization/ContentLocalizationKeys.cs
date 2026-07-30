using System;

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

        public static string ChallengeCategoryDescription(Guid challengeCategoryGuid)
        {
            return $"challengeCategory.{challengeCategoryGuid:D}.description";
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

    }
}
