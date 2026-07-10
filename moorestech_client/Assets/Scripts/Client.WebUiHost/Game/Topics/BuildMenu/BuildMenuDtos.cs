using System.Collections.Generic;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// build_menu.entries の配信 DTO
    /// Payload DTOs for build_menu.entries
    /// </summary>
    public class BuildMenuTopicDto
    {
        public List<BuildMenuEntryDto> Entries;
    }

    public class BuildMenuEntryDto
    {
        public string EntryType;
        public string EntryKey;
        public string Label;
        public string Tooltip;
        // アイコン無し（BP・BPコピー）は null でキー省略される
        // Null (thus key-omitted) for icon-less entries: blueprints and the copy tool
        public string IconUrl;
    }
}
