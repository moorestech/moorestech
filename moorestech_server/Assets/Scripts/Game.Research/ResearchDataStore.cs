using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Action;
using Game.PlayerInventory.Interface;
using Mooresmaster.Model.ResearchModule;

namespace Game.Research
{
    public class ResearchDataStore : IResearchDataStore
    {
        // 完了した研究のGUIDを保持するセット
        private readonly HashSet<Guid> _completedResearchGuids = new();

        private readonly IPlayerInventoryDataStore _inventoryDataStore;
        private readonly IGameActionExecutor _gameActionExecutor;
        private readonly ResearchEvent _researchEvent;

        public ResearchDataStore(IPlayerInventoryDataStore inventoryDataStore, IGameActionExecutor gameActionExecutor, ResearchEvent researchEvent)
        {
            _inventoryDataStore = inventoryDataStore;
            _gameActionExecutor = gameActionExecutor;
            _researchEvent = researchEvent;
        }
        
        public bool IsResearchCompleted(Guid researchGuid)
        {
            return _completedResearchGuids.Contains(researchGuid);
        }

        public bool CompleteResearch(Guid researchGuid, int playerId)
        {
            // すでに完了済みかチェック
            if (IsResearchCompleted(researchGuid)) return false;

            // 前提研究のチェック
            if (!ArePrerequisitesCompleted(researchGuid))
            {
                return false;
            }

            var researchElement = MasterHolder.ResearchMaster.GetResearch(researchGuid);
            var inventory = _inventoryDataStore.GetInventoryData(playerId);
            // プレイヤーのインベントリのアイテムチェック
            if (!CheckRequiredItems(researchElement.ConsumeItems))
            {
                return false;
            }
            
            // アイテム消費
            ConsumeItem(researchElement.ConsumeItems);
            // 研究完了記録
            _completedResearchGuids.Add(researchGuid);
            // アクション実行
            _gameActionExecutor.ExecuteActions(researchElement.ClearedActions.items);
            // イベント発火
            _researchEvent.InvokeOnResearchCompleted(playerId, researchElement);
            
            return true;
            
            #region Internal
            
            bool ArePrerequisitesCompleted(Guid targetGuid)
            {
                var element = MasterHolder.ResearchMaster.GetResearch(targetGuid);
                if (element == null) return false;

                // 可能なキーをリフレクションで吸収（生成物の差異を吸収）
                var type = element.GetType();
                var prevGuids = new List<Guid>();

                var multiProp = type.GetProperty("PrevResearchNodeGuids");
                var singleProp = type.GetProperty("PrevResearchNodeGuid");

                if (multiProp != null)
                {
                    var val = multiProp.GetValue(element);
                    if (val is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var item in enumerable)
                        {
                            if (item is Guid g)
                            {
                                prevGuids.Add(g);
                            }
                            else if (item is string s && Guid.TryParse(s, out var parsed))
                            {
                                prevGuids.Add(parsed);
                            }
                        }
                    }
                }
                else if (singleProp != null)
                {
                    var val = singleProp.GetValue(element);
                    if (val is Guid g)
                    {
                        prevGuids.Add(g);
                    }
                    else if (val is string s && Guid.TryParse(s, out var parsed))
                    {
                        prevGuids.Add(parsed);
                    }
                }

                if (prevGuids.Count == 0) return true;

                foreach (var prev in prevGuids)
                {
                    // 自身を前提に含むデータは無視
                    if (prev == Guid.Empty || prev == targetGuid) continue;
                    if (!IsResearchCompleted(prev)) return false;
                }
                return true;
            }

            bool CheckRequiredItems(ConsumeItemsElement[] consumeItems)
            {
                foreach (var consumeItem in consumeItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(consumeItem.ItemGuid);
                    var currentCount = GetItemCount( itemId);
                    
                    if (currentCount < consumeItem.ItemCount)
                        return false;
                }
                
                return true;
            }
            
            int GetItemCount(ItemId itemId)
            {
                var totalCount = 0;
                foreach (var itemStack in inventory.MainOpenableInventory.InventoryItems)
                {
                    if (itemStack.Id == itemId)
                    {
                        totalCount += itemStack.Count;
                    }
                }
                return totalCount;
            }
            
            void ConsumeItem(ConsumeItemsElement[] consumeItems)
            {
                foreach (var consumeItem in consumeItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(consumeItem.ItemGuid);
                    var remainingToConsume = consumeItem.ItemCount;
                    
                    // インベントリ内のアイテムスタックを探して消費
                    for (var i = 0; i < inventory.MainOpenableInventory.InventoryItems.Count && remainingToConsume > 0; i++)
                    {
                        var itemStack = inventory.MainOpenableInventory.InventoryItems[i];
                        if (itemStack.Id != itemId) continue;
                        
                        var consumeAmount = Math.Min(itemStack.Count, remainingToConsume);
                        var newStack = itemStack.SubItem(consumeAmount);
                        inventory.MainOpenableInventory.SetItem(i, newStack);
                        remainingToConsume -= consumeAmount;
                    }
                }
            }
            
            #endregion
        }
        
        #region SaveLoad
        
        public ResearchSaveJsonObject GetSaveJsonObject()
        {
            return new ResearchSaveJsonObject
            {
                CompletedResearchGuids = _completedResearchGuids
                    .Select(g => g.ToString())
                    .ToList()
            };
        }
        
        public void LoadResearchData(ResearchSaveJsonObject saveData)
        {
            _completedResearchGuids.Clear();
            
            if (saveData?.CompletedResearchGuids == null) return;
            
            foreach (var guidString in saveData.CompletedResearchGuids)
            {
                if (!Guid.TryParse(guidString, out var guid)) continue;
                
                _completedResearchGuids.Add(guid);
                
                // 新規追加された要素のアンロックアクションを実行
                var researchElement = MasterHolder.ResearchMaster?.GetResearch(guid);
                if (researchElement != null)
                {
                    _gameActionExecutor.ExecuteUnlockActions(researchElement.ClearedActions.items);
                }
            }
        }
        
        #endregion
    }
}
