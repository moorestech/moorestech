using System;
using System.Collections.Generic;
using System.Linq;
using Game.PlayerInventory.Interface;
using Server.Util.MessagePack;
using UnityEngine;

namespace Game.PlayerInventory
{
    /// <summary>
    /// プレイヤーIDとサブスクライブ中のインベントリの紐付けを管理する実装
    /// Implementation for managing player ID and subscribed inventory association
    /// </summary>
    public class InventorySubscriptionStore : IInventorySubscriptionStore
    {
        // プレイヤーIDごとのサブスクリプション情報を保持
        // Hold subscription information per player ID
        private readonly Dictionary<int, ISubscriptionIdentifier> _playerSubscriptions = new();
        
        // インベントリごとのサブスクライバーリストを保持
        // Hold subscriber list per inventory
        private readonly Dictionary<(InventoryType, string), HashSet<int>> _inventorySubscribers = new();
        
        
        public List<int> GetSubscribers(ISubscriptionIdentifier identifier)
        {
            // サブスクリプションキーを生成
            // Generate subscription key
            var key = CreateKey(identifier);
            if (_inventorySubscribers.TryGetValue(key, out var subscribers))
            {
                return subscribers.ToList();
            }
            return new List<int>();
        }
        
        public void Subscribe(int playerId, ISubscriptionIdentifier identifier)
        {
            // 既存のサブスクリプションがある場合は解除
            // Unsubscribe existing subscription if any
            if (_playerSubscriptions.ContainsKey(playerId))
            {
                Unsubscribe(playerId);
            }
            
            // 新しいサブスクリプションを登録
            // Register new subscription
            _playerSubscriptions[playerId] = identifier;
            
            // 登録対象インベントリのキーを生成
            // Generate key for target inventory
            var key = CreateKey(identifier);
            if (!_inventorySubscribers.ContainsKey(key))
            {
                _inventorySubscribers[key] = new HashSet<int>();
            }
            _inventorySubscribers[key].Add(playerId);
        }
        
        public void Unsubscribe(int playerId)
        {
            if (!_playerSubscriptions.TryGetValue(playerId, out var subscription))
            {
                return;
            }
            
            // サブスクライバーリストから削除するためのキーを再計算
            // Recalculate key to remove from subscriber list
            var key = CreateKey(subscription);
            if (_inventorySubscribers.TryGetValue(key, out var subscribers))
            {
                subscribers.Remove(playerId);
                if (subscribers.Count == 0)
                {
                    _inventorySubscribers.Remove(key);
                }
            }
            
            // プレイヤーのサブスクリプション情報を削除
            // Remove player's subscription information
            _playerSubscriptions.Remove(playerId);
        }
        
        public ISubscriptionIdentifier GetCurrentSubscription(int playerId)
        {
            if (_playerSubscriptions.TryGetValue(playerId, out var subscription))
            {
                return subscription;
            }
            return null;
        }
        
        
        #region Internal
        
        /// <summary>
        /// インベントリを識別するキーを生成
        /// Generate key to identify inventory
        /// </summary>
        private (InventoryType, string) CreateKey(ISubscriptionIdentifier identifier)
        {
            // 識別子を具体型に変換
            // Cast identifier to concrete type
            if (identifier is BlockInventorySubscriptionIdentifier blockIdentifier)
            {
                var position = blockIdentifier.Position;
                return (identifier.Type, $"{position.x},{position.y},{position.z}");
            }

            if (identifier is TrainInventorySubscriptionIdentifier trainIdentifier)
            {
                return (identifier.Type, trainIdentifier.TrainId.ToString());
            }
            
            throw new ArgumentException($"Invalid identifier type for InventoryType {identifier.Type}");
        }
        
        #endregion
    }
}
