using System.Collections.Generic;
using Game.Context;
using Game.PlayerInventory.Interface;
using Mooresmaster.Model.GameActionModule;
using UnityEngine;

namespace Game.Action
{
    /// <summary>
    ///     giveItem アクションの付与先を解決し、アイテムを渡す
    ///     Resolves the giveItem targets and grants the items
    /// </summary>
    internal class GiveItemGameActionExecutor
    {
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public GiveItemGameActionExecutor(IPlayerInventoryDataStore playerInventoryDataStore)
        {
            _playerInventoryDataStore = playerInventoryDataStore;
        }

        public void Execute(GameActionElement action, ActionExecutionContext context)
        {
            var param = (GiveItemGameActionParam)action.GameActionParam;
            var targetPlayerIds = ResolveTargetPlayers(param.DeliveryTarget);
            if (targetPlayerIds.Count == 0) return;

            foreach (var playerId in targetPlayerIds)
            {
                var inventoryData = _playerInventoryDataStore.GetInventoryData(playerId);
                foreach (var reward in param.RewardItems)
                {
                    var itemStack = ServerContext.ItemStackFactory.Create(reward.ItemGuid, reward.ItemCount);
                    inventoryData.MainOpenableInventory.InsertItem(itemStack);
                }
            }

            #region Internal

            List<int> ResolveTargetPlayers(string deliveryTarget)
            {
                switch (deliveryTarget)
                {
                    case GiveItemGameActionParam.DeliveryTargetConst.allPlayers:
                        return _playerInventoryDataStore.GetAllPlayerId();
                    case GiveItemGameActionParam.DeliveryTargetConst.actionInvoker:
                        if (context.HasActionInvoker)
                        {
                            return new List<int> { context.ActionInvokerPlayerId!.Value };
                        }

                        Debug.LogError("[GameActionExecutor] giveItem actionInvoker deliveryTarget requires ActionInvokerPlayerId.");
                        return new List<int>();
                    default:
                        Debug.LogError($"[GameActionExecutor] Unknown deliveryTarget: {deliveryTarget}");
                        return new List<int>();
                }
            }

            #endregion
        }
    }
}
