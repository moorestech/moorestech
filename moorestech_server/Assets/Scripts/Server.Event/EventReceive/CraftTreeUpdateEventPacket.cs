using Game.CraftTree.Data;
using Game.CraftTree.Manager;
using Game.CraftTree.Network;
using Game.Context;
using MessagePack;
using CraftTreeData = Game.CraftTree.Data.Transfer.CraftTreeData;

namespace Server.Event.EventReceive
{
    /// <summary>
    /// クラフトツリー更新イベントをクライアントに送信するクラス
    /// </summary>
    public class CraftTreeUpdateEventPacket
    {
        /// <summary>
        /// イベント識別タグ
        /// </summary>
        public const string EventTag = "va:craftTreeUpdate";
        
        private readonly CraftTreeManager _craftTreeManager;
        private readonly IClientEventSender _eventSender;
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="craftTreeManager">クラフトツリーマネージャー</param>
        /// <param name="eventSender">クライアントイベント送信者</param>
        public CraftTreeUpdateEventPacket(CraftTreeManager craftTreeManager, IClientEventSender eventSender)
        {
            _craftTreeManager = craftTreeManager;
            _eventSender = eventSender;
            
            // プレイヤーのインベントリ変更イベントを監視
            var inventoryService = ServerContext.GetService<PlayerInventoryService>();
            inventoryService.OnInventoryChanged += OnPlayerInventoryChanged;
        }
        
        /// <summary>
        /// プレイヤーのインベントリが変更された時のハンドラ
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        private void OnPlayerInventoryChanged(PlayerId playerId)
        {
            // ツリーの状態を更新
            _craftTreeManager.UpdateTreeState(playerId);
            
            // 更新があればクライアントに通知
            SendUpdates(playerId);
        }
        
        /// <summary>
        /// クラフトツリー更新をクライアントに送信
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        public void SendUpdates(PlayerId playerId)
        {
            var updates = _craftTreeManager.GetUpdatesForClient(playerId);
            if (!updates.IsEmpty())
            {
                var eventData = new CraftTreeUpdateEventMessagePack(updates);
                _eventSender.SendEvent(playerId.Value, EventTag, eventData);
            }
        }
    }
    
    /// <summary>
    /// クラフトツリー更新イベントのメッセージパック
    /// </summary>
    [MessagePackObject]
    public class CraftTreeUpdateEventMessagePack : EventMessagePackBase
    {
        /// <summary>
        /// 目標アイテムリスト
        /// </summary>
        [Key(0)]
        public GoalItemMessagePack[] GoalItems { get; set; }
        
        /// <summary>
        /// ノード更新データリスト
        /// </summary>
        [Key(1)]
        public NodeUpdateDataMessagePack[] UpdatedNodes { get; set; }
        
        /// <summary>
        /// 完全なツリーデータ（必要時のみ）
        /// </summary>
        [Key(2)]
        public CraftTreeData FullTreeData { get; set; }
        
        /// <summary>
        /// デフォルトコンストラクタ
        /// </summary>
        public CraftTreeUpdateEventMessagePack()
        {
        }
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="updateData">更新データ</param>
        public CraftTreeUpdateEventMessagePack(CraftTreeUpdateData updateData)
        {
            // 目標アイテムを変換
            if (updateData.goalItems != null)
            {
                GoalItems = new GoalItemMessagePack[updateData.goalItems.Count];
                for (int i = 0; i < updateData.goalItems.Count; i++)
                {
                    var goalItem = updateData.goalItems[i];
                    GoalItems[i] = new GoalItemMessagePack
                    {
                        ItemId = goalItem.itemId,
                        RequiredCount = goalItem.requiredCount,
                        AvailableCount = goalItem.availableCount
                    };
                }
            }
            
            // 更新ノードを変換
            if (updateData.updatedNodes != null)
            {
                UpdatedNodes = new NodeUpdateDataMessagePack[updateData.updatedNodes.Count];
                for (int i = 0; i < updateData.updatedNodes.Count; i++)
                {
                    var node = updateData.updatedNodes[i];
                    UpdatedNodes[i] = new NodeUpdateDataMessagePack
                    {
                        NodeItemId = node.nodeItemId,
                        NewState = node.newState,
                        NewProgress = node.newProgress
                    };
                }
            }
            
            // ツリーデータをそのまま設定
            FullTreeData = updateData.fullTreeData;
        }
        
        /// <summary>
        /// CraftTreeUpdateDataを取得
        /// </summary>
        /// <returns>更新データ</returns>
        public CraftTreeUpdateData ToUpdateData()
        {
            var result = new CraftTreeUpdateData
            {
                fullTreeData = FullTreeData
            };
            
            // 目標アイテムを変換
            if (GoalItems != null)
            {
                foreach (var goalItemPack in GoalItems)
                {
                    var goalItem = new GoalItem(
                        goalItemPack.ItemId,
                        goalItemPack.RequiredCount,
                        goalItemPack.AvailableCount
                    );
                    result.goalItems.Add(goalItem);
                }
            }
            
            // 更新ノードを変換
            if (UpdatedNodes != null)
            {
                foreach (var nodePack in UpdatedNodes)
                {
                    var nodeUpdate = new NodeUpdateData(
                        nodePack.NodeItemId,
                        nodePack.NewState,
                        nodePack.NewProgress
                    );
                    result.updatedNodes.Add(nodeUpdate);
                }
            }
            
            return result;
        }
    }
    
    /// <summary>
    /// 目標アイテムのメッセージパック
    /// </summary>
    [MessagePackObject]
    public class GoalItemMessagePack
    {
        /// <summary>
        /// アイテムID
        /// </summary>
        [Key(0)]
        public Core.Item.ItemId ItemId { get; set; }
        
        /// <summary>
        /// 必要数量
        /// </summary>
        [Key(1)]
        public int RequiredCount { get; set; }
        
        /// <summary>
        /// 現在利用可能な数量
        /// </summary>
        [Key(2)]
        public int AvailableCount { get; set; }
    }
    
    /// <summary>
    /// ノード更新データのメッセージパック
    /// </summary>
    [MessagePackObject]
    public class NodeUpdateDataMessagePack
    {
        /// <summary>
        /// ノードのアイテムID
        /// </summary>
        [Key(0)]
        public Core.Item.ItemId NodeItemId { get; set; }
        
        /// <summary>
        /// 新しい状態
        /// </summary>
        [Key(1)]
        public NodeState NewState { get; set; }
        
        /// <summary>
        /// 新しい進捗値
        /// </summary>
        [Key(2)]
        public int NewProgress { get; set; }
    }
}