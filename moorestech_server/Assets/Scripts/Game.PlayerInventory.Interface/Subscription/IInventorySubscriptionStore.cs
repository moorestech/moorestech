using System.Collections.Generic;

namespace Game.PlayerInventory.Interface.Subscription
{
    /// <summary>
    /// プレイヤーIDとサブスクライブ中のインベントリの紐付けを管理するインターフェース
    /// Interface for managing player ID and subscribed inventory association
    /// </summary>
    public interface IInventorySubscriptionStore
    {
        List<int> GetSubscribers(ISubInventoryIdentifier identifier);
        
        void Subscribe(int playerId, ISubInventoryIdentifier identifier);
        
        void Unsubscribe(int playerId, ISubInventoryIdentifier identifier);
    }
}
