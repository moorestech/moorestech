using Core.Item;
using Core.Master;
using Game.CraftTree.Data;

namespace Game.CraftTree.Network
{
    /// <summary>
    /// クラフトツリーノードの更新情報を表すクラス
    /// サーバーからクライアントへの差分更新に使用
    /// </summary>
    public class NodeUpdateData
    {
        /// <summary>
        /// 更新対象ノードのアイテムID
        /// </summary>
        public ItemId nodeItemId { get; set; }
        
        /// <summary>
        /// 新しい状態
        /// </summary>
        public NodeState newState { get; set; }
        
        /// <summary>
        /// 新しい進捗値（現在の数量）
        /// </summary>
        public int newProgress { get; set; }
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="nodeItemId">更新対象ノードのアイテムID</param>
        /// <param name="newState">新しい状態</param>
        /// <param name="newProgress">新しい進捗値</param>
        public NodeUpdateData(ItemId nodeItemId, NodeState newState, int newProgress)
        {
            this.nodeItemId = nodeItemId;
            this.newState = newState;
            this.newProgress = newProgress;
        }
        
        /// <summary>
        /// デフォルトコンストラクタ（シリアライズ用）
        /// </summary>
        public NodeUpdateData() { }
    }
}