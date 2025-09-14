using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Action;
using Game.PlayerInventory.Interface;
using Game.Research.Interface;
using Mooresmaster.Model.ChallengeActionModule;
using Mooresmaster.Model.ResearchModule;

namespace Game.Research
{
    // Mooresmaster.Model.ResearchModuleから生成される想定の型
    // 実際のSourceGeneratorで生成される型名に合わせて調整が必要な場合があります
    public class ConsumeItem
    {
        public Guid ItemGuid { get; set; }
        public int ItemCount { get; set; }
    }

    public class ResearchDataStore : IResearchDataStore
    {
        // ワールド全体で共有される完了済み研究のセット
        private readonly HashSet<Guid> _completedResearchGuids = new();

        private readonly IPlayerInventoryDataStore _inventoryDataStore;
        private readonly IGameActionExecutor _gameActionExecutor;
        private readonly ResearchEvent _researchEvent;

        public ResearchDataStore(
            IPlayerInventoryDataStore inventoryDataStore,
            IGameActionExecutor gameActionExecutor,
            ResearchEvent researchEvent)
        {
            _inventoryDataStore = inventoryDataStore;
            _gameActionExecutor = gameActionExecutor;
            _researchEvent = researchEvent;
        }

        public bool IsResearchCompleted(Guid researchGuid)
        {
            return _completedResearchGuids.Contains(researchGuid);
        }

        public bool CanCompleteResearch(Guid researchGuid, int playerId)
        {
            var researchElement = MasterHolder.ResearchMaster?.GetResearch(researchGuid);
            if (researchElement == null) return false;

            // 既に完了済みチェック
            if (_completedResearchGuids.Contains(researchGuid))
                return false;

            // 前提研究チェック
            if (researchElement.PrevResearchNodeGuid != Guid.Empty &&
                !_completedResearchGuids.Contains(researchElement.PrevResearchNodeGuid))
                return false;

            // アイテム所持チェック（プレイヤーのインベントリから）
            var consumeItems = GetConsumeItems(researchElement);
            if (!CheckRequiredItems(playerId, consumeItems))
                return false;

            return true;
        }

        public ResearchCompletionResult CompleteResearch(Guid researchGuid, int playerId)
        {
            if (!CanCompleteResearch(researchGuid, playerId))
            {
                _researchEvent.PublishResearchFailed(playerId, researchGuid, "Research cannot be completed");
                return new ResearchCompletionResult
                {
                    Success = false,
                    Reason = "Research cannot be completed"
                };
            }

            var researchElement = MasterHolder.ResearchMaster.GetResearch(researchGuid);

            // プレイヤーのインベントリからアイテムを消費
            var consumeItems = GetConsumeItems(researchElement);
            if (!ConsumeRequiredItems(playerId, consumeItems))
            {
                _researchEvent.PublishResearchFailed(playerId, researchGuid, "Failed to consume required items");
                return new ResearchCompletionResult
                {
                    Success = false,
                    Reason = "Failed to consume required items"
                };
            }

            // 研究完了記録（ワールドレベル）
            _completedResearchGuids.Add(researchGuid);

            // アクション実行（GameActionExecutorを使用）
            ExecuteResearchActions(researchElement.ClearedActions);

            // イベント発行
            _researchEvent.PublishResearchCompleted(playerId, researchGuid);

            return new ResearchCompletionResult
            {
                Success = true,
                CompletedResearchGuid = researchGuid
            };
        }

        public HashSet<Guid> GetCompletedResearchGuids()
        {
            return new HashSet<Guid>(_completedResearchGuids);
        }

        private void ExecuteResearchActions(ChallengeActionElement[] actions)
        {
            if (actions == null) return;

            // GameActionExecutorを使用してアクションを実行
            foreach (var action in actions)
            {
                _gameActionExecutor.ExecuteAction(action);
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

            if (saveData?.CompletedResearchGuids != null)
            {
                foreach (var guidString in saveData.CompletedResearchGuids)
                {
                    if (Guid.TryParse(guidString, out var guid))
                    {
                        _completedResearchGuids.Add(guid);

                        // 新規追加された要素のアンロックアクションを実行
                        var researchElement = MasterHolder.ResearchMaster?.GetResearch(guid);
                        if (researchElement != null)
                        {
                            ExecuteUnlockActions(researchElement.ClearedActions);
                        }
                    }
                }
            }
        }

        private void ExecuteUnlockActions(ChallengeActionElement[] actions)
        {
            if (actions == null) return;

            // ロード時はアンロック系アクションのみ実行
            foreach (var action in actions)
            {
                switch (action.ChallengeActionType)
                {
                    case ChallengeActionElement.ChallengeActionTypeConst.unlockCraftRecipe:
                    case ChallengeActionElement.ChallengeActionTypeConst.unlockItemRecipeView:
                    case ChallengeActionElement.ChallengeActionTypeConst.unlockChallengeCategory:
                        _gameActionExecutor.ExecuteAction(action);
                        break;
                }
            }
        }

        #endregion

        #region Private Methods

        private ConsumeItem[] GetConsumeItems(ResearchNodeMasterElement researchElement)
        {
            // TODO: SourceGeneratorで生成される実際の型に合わせて調整
            // 現時点では空の配列を返す
            if (researchElement == null)
                return new ConsumeItem[0];

            // researchElement.ConsumeItemsから変換
            // プロパティ名が小文字のconsumeItemsの可能性もある
            return new ConsumeItem[0];
        }

        private bool CheckRequiredItems(int playerId, ConsumeItem[] consumeItems)
        {
            if (consumeItems == null || consumeItems.Length == 0)
                return true;

            var inventory = _inventoryDataStore.GetInventoryData(playerId);
            if (inventory == null)
                return false;

            foreach (var consumeItem in consumeItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(consumeItem.ItemGuid);
                var currentCount = GetItemCount(inventory.MainOpenableInventory, itemId);

                if (currentCount < consumeItem.ItemCount)
                    return false;
            }

            return true;
        }

        private bool ConsumeRequiredItems(int playerId, ConsumeItem[] consumeItems)
        {
            if (consumeItems == null || consumeItems.Length == 0)
                return true;

            var inventory = _inventoryDataStore.GetInventoryData(playerId);
            if (inventory == null)
                return false;

            try
            {
                foreach (var consumeItem in consumeItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(consumeItem.ItemGuid);
                    var remainingToConsume = consumeItem.ItemCount;

                    // インベントリ内のアイテムスタックを探して消費
                    for (int i = 0; i < inventory.MainOpenableInventory.InventoryItems.Count && remainingToConsume > 0; i++)
                    {
                        var itemStack = inventory.MainOpenableInventory.InventoryItems[i];
                        if (itemStack.Id.Equals(itemId))
                        {
                            var consumeAmount = Math.Min(itemStack.Count, remainingToConsume);
                            var newStack = itemStack.SubItem(consumeAmount);
                            inventory.MainOpenableInventory.SetItem(i, newStack);
                            remainingToConsume -= consumeAmount;
                        }
                    }

                    if (remainingToConsume > 0)
                    {
                        // アイテムが不足している
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private int GetItemCount(IOpenableInventory inventory, ItemId itemId)
        {
            int totalCount = 0;
            foreach (var itemStack in inventory.InventoryItems)
            {
                if (itemStack.Id.Equals(itemId))
                {
                    totalCount += itemStack.Count;
                }
            }
            return totalCount;
        }

        #endregion
    }
}