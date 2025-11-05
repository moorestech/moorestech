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
        private readonly Dictionary<int, (InventoryType type, object identifier)> _playerSubscriptions = new();
        
        // インベントリごとのサブスクライバーリストを保持
        // Hold subscriber list per inventory
        private readonly Dictionary<(InventoryType, string), HashSet<int>> _inventorySubscribers = new();
        
        
        public List<int> GetSubscribers(InventoryType type, object identifier)
        {
            var key = CreateKey(type, identifier);
            if (_inventorySubscribers.TryGetValue(key, out var subscribers))
            {
                return subscribers.ToList();
            }
            return new List<int>();
        }
        
        public void Subscribe(int playerId, InventoryType type, object identifier)
        {
            // 既存のサブスクリプションがある場合は解除
            // Unsubscribe existing subscription if any
            if (_playerSubscriptions.ContainsKey(playerId))
            {
                Unsubscribe(playerId);
            }
            
            // 新しいサブスクリプションを登録
            // Register new subscription
            _playerSubscriptions[playerId] = (type, identifier);
            
            var key = CreateKey(type, identifier);
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
            
            // サブスクライバーリストから削除
            // Remove from subscriber list
            var key = CreateKey(subscription.type, subscription.identifier);
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
        
        public (InventoryType type, object identifier)? GetCurrentSubscription(int playerId)
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
        private (InventoryType, string) CreateKey(InventoryType type, object identifier)
        {
            string identifierStr = type switch
            {
                InventoryType.Block when identifier is Vector3Int blockPos => $"{blockPos.x},{blockPos.y},{blockPos.z}",
                InventoryType.Train when identifier is Guid trainId => trainId.ToString(),
                _ => throw new ArgumentException($"Invalid identifier type for InventoryType {type}")
            };
            
            return (type, identifierStr);
        }
        
        #endregion
    }
}

