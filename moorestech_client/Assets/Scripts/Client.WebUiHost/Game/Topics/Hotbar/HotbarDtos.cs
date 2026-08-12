using System.Collections.Generic;

namespace Client.WebUiHost.Game.Topics.Hotbar
{
    /// <summary>
    /// local_player.hotbar の配信 DTO
    /// Payload DTOs for local_player.hotbar
    /// </summary>
    public class HotbarTopicDto
    {
        public List<HotbarSlotDto> Slots;
        public int SelectedSlot;
    }

    public class HotbarSlotDto
    {
        // build_menu.entries と同じGuid1本+kind語彙を共有する
        // Shares the same single-GUID id + kind vocabulary with build_menu.entries
        public string Id;
        public string Kind;
        public string Label;

        // アイコン無し種別（BP・BPコピー）は null でキー省略される
        // Null (thus key-omitted) for icon-less kinds: blueprints and the copy tool
        public string IconUrl;
    }
}
