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
        // 設置対象の同一性はGuid文字列のId1本が持ち、Kindは表示・振る舞いの分類にすぎない
        // Identity lives solely in Id (a GUID string); Kind only classifies display and behavior
        public string Id;
        public string Kind;

        // マスタ由来名はWeb辞書で解決し、ユーザー命名BPだけLabelを配信する
        // Master-derived names resolve through the Web dictionary; only user-named blueprints carry Label
        public string Label;
        public string CategoryGuid;
        public string SubCategoryGuid;
        public List<BuildMenuRequiredItemDto> RequiredItems;

        // 財布を使うブロックだけが設置数/1セットと残り設置数を持つ。判定はホスト側の財布が済ませ、null でキー省略される
        // Only wallet-backed blocks carry the per-set count and the remainder; the host-side wallet decides, and null omits the key
        public BuildMenuSetPlacementDto SetPlacement;

        // アイコン無し（BP・BPコピー）は null でキー省略される
        // Null (thus key-omitted) for icon-less entries: blueprints and the copy tool
        public string IconUrl;
    }

    public class BuildMenuRequiredItemDto
    {
        public int ItemId;
        public int Count;
    }

    public class BuildMenuSetPlacementDto
    {
        public int PerCost;
        public int Remaining;
    }
}
