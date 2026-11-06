using System;

namespace Client.Ui.State.BuildMenu.Session
{
    /// <summary>
    /// 閉じて開き直しても検索・位置・ホバーを戻すためのストア（Web buildMenuSessionState.ts の写し。寿命はゲーム）
    /// Store that brings back search, position and hover on reopen (port of the Web buildMenuSessionState.ts; game lifetime)
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
