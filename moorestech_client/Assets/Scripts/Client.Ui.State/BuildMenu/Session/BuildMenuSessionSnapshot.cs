using System;

namespace Client.Ui.State.BuildMenu.Session
{
    /// <summary>
    /// セッション内状態（現在地は位置から再現）
    /// In-session state; the current category derives from position
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
