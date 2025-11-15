using System;
using System.Collections.Generic;
using System.Linq;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Subscription;
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
        // プレイヤーIDごとのサブスクリプション情報を保持（複数サブスクリプション対応）
        // Hold subscription information per player ID (supports multiple subscriptions)
        private readonly Dictionary<int, HashSet<ISubInventoryIdentifier>> _playerSubscriptions = new();
        
        // インベントリごとのサブスクライバーリストを保持
        // Hold subscriber list per inventory
        private readonly Dictionary<(InventoryType, string), HashSet<int>> _inventorySubscribers = new();
        
        
        public List<int> GetSubscribers(ISubInventoryIdentifier identifier)
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
        
        public void Subscribe(int playerId, ISubInventoryIdentifier identifier)
        {
            // プレイヤーのサブスクリプションリストを取得または作成
            // Get or create player's subscription list
            if (!_playerSubscriptions.ContainsKey(playerId))
            {
                _playerSubscriptions[playerId] = new HashSet<ISubInventoryIdentifier>();
            }

            // 新しいサブスクリプションを追加
            // Add new subscription
            _playerSubscriptions[playerId].Add(identifier);

            // 登録対象インベントリのキーを生成
            // Generate key for target inventory
            var key = CreateKey(identifier);
            if (!_inventorySubscribers.ContainsKey(key))
            {
                _inventorySubscribers[key] = new HashSet<int>();
            }
            _inventorySubscribers[key].Add(playerId);
        }
        
        public void Unsubscribe(int playerId, ISubInventoryIdentifier identifier)
        {
            if (!_playerSubscriptions.TryGetValue(playerId, out var subscriptions)) return;

            // 指定されたサブスクリプションを削除
            // Remove the specified subscription
            if (!subscriptions.Remove(identifier)) return;

            // サブスクライバーリストから削除するためのキーを計算
            // Calculate key to remove from subscriber list
            var key = CreateKey(identifier);
            if (!_inventorySubscribers.TryGetValue(key, out var subscribers)) return;
            
            subscribers.Remove(playerId);
            if (subscribers.Count == 0)
            {
                _inventorySubscribers.Remove(key);
            }
        }
        
        
        /// <summary>
        /// インベントリを識別するキーを生成
        /// Generate key to identify inventory
        /// </summary>
        private static (InventoryType, string) CreateKey(ISubInventoryIdentifier identifier)
        {
            switch (identifier.Type)
            {
                case InventoryType.Block:
                    var position = ((BlockInventorySubInventoryIdentifier) identifier).Position;
                    return (identifier.Type, $"{position.x},{position.y},{position.z}");
                case InventoryType.Train:
                    var carId = ((TrainInventorySubInventoryIdentifier) identifier).TrainCarId;
                    return (identifier.Type, carId.ToString());
                default:
                    throw new ArgumentException($"Invalid identifier type for InventoryType {identifier.Type}");
            }
        }
    }
}
