using Core.Item;
using Core.Master;

namespace Game.CraftTree.Data
{
    /// <summary>
    /// HUDに表示される目標アイテム情報を表すクラス
    /// </summary>
    public class GoalItem
    {
        /// <summary>
        /// アイテムID
        /// </summary>
        public ItemId itemId { get; private set; }
        
        /// <summary>
        /// 必要数量
        /// </summary>
        public int requiredCount { get; private set; }
        
        /// <summary>
        /// 現在利用可能な数量（インベントリの数量）
        /// </summary>
        public int availableCount { get; private set; }
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="itemId">アイテムID</param>
        /// <param name="requiredCount">必要数量</param>
        /// <param name="availableCount">現在利用可能な数量</param>
        public GoalItem(ItemId itemId, int requiredCount, int availableCount)
        {
            this.itemId = itemId;
            this.requiredCount = requiredCount;
            this.availableCount = availableCount;
        }
        
        /// <summary>
        /// 目標が達成されているかどうか
        /// </summary>
        /// <returns>利用可能な数量が必要数量以上の場合はtrue</returns>
        public bool IsCompleted()
        {
            return availableCount >= requiredCount;
        }
        
        /// <summary>
        /// 達成率（0.0〜1.0）を計算
        /// </summary>
        /// <returns>達成率（0.0〜1.0）</returns>
        public float GetCompletionRate()
        {
            if (requiredCount <= 0)
                return 1.0f;
                
            return (float)System.Math.Min(availableCount, requiredCount) / requiredCount;
        }
    }
}