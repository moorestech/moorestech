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
            if (IsResearchCompleted(researchGuid))
            {
                return false;
            }

            var researchElement = MasterHolder.ResearchMaster.GetResearch(researchGuid);
            var inventory = _inventoryDataStore.GetInventoryData(playerId);
            var nodeState = EvaluateResearchNodeState(researchElement, inventory);
            if (nodeState != ResearchNodeState.Researchable)
            {
                return false;
            }

            ConsumeItems(researchElement.ConsumeItems, inventory);
            _completedResearchGuids.Add(researchGuid);
            _gameActionExecutor.ExecuteActions(researchElement.ClearedActions.items, ActionExecutionContext.ForPlayer(playerId));
            _researchEvent.InvokeOnResearchCompleted(playerId, researchElement);

            return true;
        }

        public Dictionary<Guid, ResearchNodeState> GetResearchNodeStates(int playerId)
        {
            var inventory = _inventoryDataStore.GetInventoryData(playerId);
            var researchElements = MasterHolder.ResearchMaster.GetAllResearches();

            var nodeStates = new Dictionary<Guid, ResearchNodeState>(researchElements.Count);
            foreach (var researchElement in researchElements)
            {
                nodeStates[researchElement.ResearchNodeGuid] = EvaluateResearchNodeState(researchElement, inventory);
            }

            return nodeStates;
        }


        private ResearchNodeState EvaluateResearchNodeState(ResearchNodeMasterElement researchElement, PlayerInventoryData inventory)
        {
            if (IsResearchCompleted(researchElement.ResearchNodeGuid))
            {
                return ResearchNodeState.Completed;
            }

            var prerequisitesCompleted = ArePrerequisitesCompleted(researchElement.PrevResearchNodeGuids, researchElement.ResearchNodeGuid);
            var hasRequiredItems = HasRequiredItems(researchElement.ConsumeItems);

            if (prerequisitesCompleted && hasRequiredItems)
            {
                return ResearchNodeState.Researchable;
            }

            if (!prerequisitesCompleted && !hasRequiredItems)
            {
                return ResearchNodeState.UnresearchableAllReasons;
            }

            if (!prerequisitesCompleted)
            {
                return ResearchNodeState.UnresearchableNotEnoughPreNode;
            }

            return ResearchNodeState.UnresearchableNotEnoughItem;
            
            #region Internal
            
            bool ArePrerequisitesCompleted(Guid[] prerequisiteGuids, Guid currentResearchGuid)
            {
                if (prerequisiteGuids == null || prerequisiteGuids.Length == 0)
                {
                    return true;
                }
                
                foreach (var prerequisite in prerequisiteGuids)
                {
                    if (prerequisite == currentResearchGuid)
                    {
                        continue;
                    }
                    
                    if (!_completedResearchGuids.Contains(prerequisite))
                    {
                        return false;
                    }
                }
                
                return true;
            }
            
            bool HasRequiredItems(ConsumeItemsElement[] consumeItems)
            {
                if (consumeItems == null || consumeItems.Length == 0)
                {
                    return true;
                }
                
                foreach (var consumeItem in consumeItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(consumeItem.ItemGuid);
                    var currentCount = GetItemCount(itemId);
                    
                    if (currentCount < consumeItem.ItemCount)
                    {
                        return false;
                    }
                }
                
                return true;
            }
            
            int GetItemCount(ItemId itemId)
            {
                var totalCount = 0;
                foreach (var itemStack in inventory.MainOpenableInventory.InventoryItems)
                {
                    if (itemStack.Id != itemId)
                    {
                        continue;
                    }
                    
                    totalCount += itemStack.Count;
                }
                
                return totalCount;
            }
            
            #endregion
        }

        private void ConsumeItems(ConsumeItemsElement[] consumeItems, PlayerInventoryData inventory)
        {
            if (consumeItems == null || consumeItems.Length == 0)
            {
                return;
            }

            foreach (var consumeItem in consumeItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(consumeItem.ItemGuid);
                var remainingToConsume = consumeItem.ItemCount;

                for (var i = 0; i < inventory.MainOpenableInventory.InventoryItems.Count && remainingToConsume > 0; i++)
                {
                    var itemStack = inventory.MainOpenableInventory.InventoryItems[i];
                    if (itemStack.Id != itemId)
                    {
                        continue;
                    }

                    var consumeAmount = Math.Min(itemStack.Count, remainingToConsume);
                    var newStack = itemStack.SubItem(consumeAmount);
                    inventory.MainOpenableInventory.SetItem(i, newStack);
                    remainingToConsume -= consumeAmount;
                }
            }
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
