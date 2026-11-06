using System;

namespace Client.Ui.State.BuildMenu.Session
{
    /// <summary>
    /// ビルドメニューのセッション内状態。現在地は位置から再現できるのでカテゴリは持たない
    /// In-session build-menu state; the current category is derivable from the position, so it is not stored
    /// </summary>
    public sealed class BuildMenuSessionSnapshot
    {
        public static readonly BuildMenuSessionSnapshot Initial = new("", 0f, null);

        public string Query { get; }
        public float ScrollTop { get; }
        public Guid? HoveredEntryId { get; }

        public BuildMenuSessionSnapshot(string query, float scrollTop, Guid? hoveredEntryId)
        {
            Query = query;
            ScrollTop = scrollTop;
            HoveredEntryId = hoveredEntryId;
        }
    }
}
