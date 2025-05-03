using System;
using System.Collections.Generic;
using Core.Item;
using Core.Master;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;

namespace Game.CraftTree.Manager
{
    /// <summary>
    /// プレイヤーのインベントリ情報を提供するサービス
    /// </summary>
    public class PlayerInventoryService
    {
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        
        /// <summary>
        /// インベントリ変更時のイベント
        /// </summary>
        public event Action<PlayerId> OnInventoryChanged;
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="playerInventoryDataStore">プレイヤーインベントリデータストア</param>
        public PlayerInventoryService(IPlayerInventoryDataStore playerInventoryDataStore)
        {
            _playerInventoryDataStore = playerInventoryDataStore ?? throw new ArgumentNullException(nameof(playerInventoryDataStore));
            
            // インベントリ更新イベントの購読
            if (_playerInventoryDataStore is IMainInventoryUpdateEvent mainInventoryUpdateEvent)
            {
                mainInventoryUpdateEvent.OnMainInventoryUpdate += HandleInventoryUpdate;
            }
            
            if (_playerInventoryDataStore is IGrabInventoryUpdateEvent grabInventoryUpdateEvent)
            {
                grabInventoryUpdateEvent.OnGrabInventoryUpdate += HandleInventoryUpdate;
            }
        }
        
        /// <summary>
        /// インベントリ更新イベントハンドラ
        /// </summary>
        /// <param name="properties">更新イベントのプロパティ</param>
        private void HandleInventoryUpdate(PlayerInventoryUpdateEventProperties properties)
        {
            // イベント通知
            OnInventoryChanged?.Invoke(new PlayerId(properties.PlayerId));
        }
        
        /// <summary>
        /// プレイヤーのインベントリアイテムを取得
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <returns>アイテムIDと所持数のディクショナリ</returns>
        public Dictionary<ItemId, int> GetInventoryItems(PlayerId playerId)
        {
            var result = new Dictionary<ItemId, int>();
            
            // メインインベントリからアイテムを取得
            var inventoryData = _playerInventoryDataStore.GetPlayerInventoryData(playerId.Value);
            if (inventoryData != null)
            {
                // メインインベントリのアイテムを集計
                for (int i = 0; i < inventoryData.Main.Length; i++)
                {
                    var itemStack = inventoryData.Main[i];
                    if (itemStack != null && !itemStack.IsEmpty())
                    {
                        var itemId = itemStack.itemId;
                        if (result.TryGetValue(itemId, out int count))
                        {
                            result[itemId] = count + itemStack.count;
                        }
                        else
                        {
                            result[itemId] = itemStack.count;
                        }
                    }
                }
                
                // グラブインベントリのアイテムも含める
                if (inventoryData.Grab != null && !inventoryData.Grab.IsEmpty())
                {
                    var itemId = inventoryData.Grab.itemId;
                    if (result.TryGetValue(itemId, out int count))
                    {
                        result[itemId] = count + inventoryData.Grab.count;
                    }
                    else
                    {
                        result[itemId] = inventoryData.Grab.count;
                    }
                }
            }
            
            return result;
        }
    }
}