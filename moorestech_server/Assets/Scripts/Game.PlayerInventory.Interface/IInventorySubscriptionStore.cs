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
        List<int> GetSubscribers(ISubscriptionIdentifier identifier);
        
        void Subscribe(int playerId, ISubscriptionIdentifier identifier);
        
        void Unsubscribe(int playerId);
    }
}
