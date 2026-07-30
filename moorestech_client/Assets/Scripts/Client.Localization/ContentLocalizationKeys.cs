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
    }
}
