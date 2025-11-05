using System.Collections.Generic;
using Server.Util.MessagePack;

namespace Game.PlayerInventory.Interface
{
    /// <summary>
    /// プレイヤーIDとサブスクライブ中のインベントリの紐付けを管理するインターフェース
    /// Interface for managing player ID and subscribed inventory association
    /// </summary>
    public interface IInventorySubscriptionStore
    {
        /// <summary>
        /// 指定したインベントリをサブスクライブしているプレイヤーIDのリストを取得
        /// Get list of player IDs subscribing to the specified inventory
        /// </summary>
        List<int> GetSubscribers(ISubscriptionIdentifier identifier);
        
        /// <summary>
        /// プレイヤーがインベントリをサブスクライブ
        /// Player subscribes to inventory
        /// </summary>
        void Subscribe(int playerId, ISubscriptionIdentifier identifier);
        
        /// <summary>
        /// プレイヤーのサブスクリプションを解除
        /// Unsubscribe player's subscription
        /// </summary>
        void Unsubscribe(int playerId);
        
        /// <summary>
        /// プレイヤーが現在サブスクライブしているインベントリ情報を取得
        /// Get currently subscribed inventory information for player
        /// </summary>
        ISubscriptionIdentifier GetCurrentSubscription(int playerId);
    }
}
