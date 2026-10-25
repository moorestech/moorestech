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

            if (!ResearchConsumeItemsUtil.TryConsume(researchElement.ConsumeItems, inventory.MainOpenableInventory, out _)) return false;
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
            var hasRequiredItems = ResearchConsumeItemsUtil.HasRequiredItems(researchElement.ConsumeItems, inventory.MainOpenableInventory.InventoryItems);

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
                // 読めない値は完了に数えない。刈り込みを通らない読み込み経路でも気づけるよう警告する
                // An unreadable value is not counted as completed; warn so load paths that skip the pruner still surface it
                if (!Guid.TryParse(guidString, out var guid))
                {
                    UnityEngine.Debug.LogWarning($"研究の完了一覧に GUID として読めない値があるため読み飛ばします。 value={guidString}");
                    continue;
                }

                _completedResearchGuids.Add(guid);

                // マスタに無い研究は解放をやり直せない
                // A research missing from the master cannot replay its unlocks
                var researchElement = MasterHolder.ResearchMaster?.GetResearch(guid);
                if (researchElement == null)
                {
                    UnityEngine.Debug.LogWarning($"完了済みの研究がマスタに無いため解放のやり直しを見送ります。 researchGuid={guid}");
                    continue;
                }

                // 新規追加された要素のアンロックアクションを実行
                // Replay unlock actions so newly added elements get unlocked
                _gameActionExecutor.ExecuteUnlockActions(researchElement.ClearedActions.items);
            }
        }
        
        #endregion
    }
}
