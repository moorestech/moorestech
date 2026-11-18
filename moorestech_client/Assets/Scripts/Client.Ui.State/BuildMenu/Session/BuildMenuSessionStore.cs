using System;

namespace Client.Ui.State.BuildMenu.Session
{
    /// <summary>
    /// 検索・位置・ホバーの再現（Web 写し・寿命はゲーム）
    /// Restores search, position and hover (Web port; game lifetime)
    /// </summary>
    public sealed class BuildMenuSessionStore
    {
        public BuildMenuSessionSnapshot Current { get; private set; } = BuildMenuSessionSnapshot.Initial;

        public void SetQuery(string query)
        {
            Current = new BuildMenuSessionSnapshot(query, Current.ScrollTop, Current.HoveredEntryId);
        }

        public void SetScrollTop(float scrollTop)
        {
            Current = new BuildMenuSessionSnapshot(Current.Query, scrollTop, Current.HoveredEntryId);
        }

        // null でホバーを解く
        // Null clears the hover
        public void SetHoveredEntry(Guid? entryId)
        {
            Current = new BuildMenuSessionSnapshot(Current.Query, Current.ScrollTop, entryId);
        }
    }
}
