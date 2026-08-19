using System.Collections.Generic;

namespace Client.WebUiHost.Game.Topics.Hotbar
{
    /// <summary>
    /// local_player.hotbar の配信 DTO。枠が持つのは配置対象9件のみで、持ち物のアイテムは入らない
    /// Payload DTOs for local_player.hotbar; the 9 slots hold placement targets only, never inventory items
    /// </summary>
    public class HotbarTopicDto
    {
        public List<HotbarSlotDto> Slots;
        public int SelectedSlot;
    }

    public class HotbarSlotDto
    {
        // build_menu.entries と同じGuid1本+kind語彙を共有し、解決できない枠だけ kind="unresolved" を足す
        // Shares the same single-GUID id + kind vocabulary with build_menu.entries, adding kind="unresolved" for slots that cannot be resolved
        public string Id;
        public string Kind;

        // ユーザー命名のBPだけが原文名を運ぶ。マスタ由来名はwebが辞書で解決する
        // Only user-named blueprints carry their raw name; master-derived names are resolved by the web dictionary
        public string Label;

        // アイコン無し種別（BP・BPコピー・未解決）は null でキー省略される
        // Null (thus key-omitted) for icon-less kinds: blueprints, the copy tool, and unresolved slots
        public string IconUrl;
    }
}
