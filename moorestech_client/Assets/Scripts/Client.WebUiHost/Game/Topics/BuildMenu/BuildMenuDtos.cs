using System.Collections.Generic;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// build_menu.entries の配信 DTO
    /// Payload DTOs for build_menu.entries
    /// </summary>
    public class BuildMenuTopicDto
    {
        public List<BuildMenuCategoryDto> Categories;
        public List<BuildMenuEntryDto> Entries;
    }

    public class BuildMenuCategoryDto
    {
        public string CategoryGuid;
        public List<string> SubCategoryGuids;
    }

    public class BuildMenuEntryDto
    {
        public string EntryType;
        public string EntryKey;
        public string Label;
        public string CategoryGuid;
        public string SubCategoryGuid;
        public List<BuildMenuRequiredItemDto> RequiredItems;

        // アイコン無し（BP・BPコピー）は null でキー省略される
        // Null (thus key-omitted) for icon-less entries: blueprints and the copy tool
        public string IconUrl;
    }

    public class BuildMenuRequiredItemDto
    {
        public int ItemId;
        public int Count;
    }
}
