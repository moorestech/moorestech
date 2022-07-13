namespace Game.Crafting.Interface
{
    /// <summary>
    /// アイテムクラフトの実行を代行するサービスです。
    /// </summary>
    public interface IItemCraftingService
    {
        /// <summary>
        /// クラフトスロットを1回クリックしてクラフトする通常の方法
        /// </summary>
        public void NormalCraft();
        /// <summary>
        /// シフト+クリックで作れるだけ作るクラフト
        /// </summary>
        public void AllCraft();
        /// <summary>
        /// コントロール＋クリックで1スタック作るクラフト
        /// </summary>
        public void OneStackCraft();
    }
}