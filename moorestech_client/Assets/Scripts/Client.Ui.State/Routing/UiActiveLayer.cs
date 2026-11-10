namespace Client.Ui.State.Routing
{
    /// <summary>
    /// ゲームの層を覆う画面かの判定。Web の deriveActiveLayer（modal > blockInventory > research > buildMenu > game）の写し
    /// Whether a screen covers the game layer; a copy of the web deriveActiveLayer (modal > blockInventory > research > buildMenu > game)
    /// </summary>
    public static class UiActiveLayer
    {
        public static bool CoversGameLayer(UiScreen screen, bool modalPending)
        {
            // モーダルの保留は画面に関わらず前面。画面の分類は性質の表の列が持ち、行の書き忘れは表の静的初期化が止める
            // A pending modal is frontmost on any screen; the trait column classifies screens and a missing row stops at table initialization
            if (modalPending) return true;
            return UiScreenTraitsTable.Get(screen).CoversGameLayer;
        }
    }
}
