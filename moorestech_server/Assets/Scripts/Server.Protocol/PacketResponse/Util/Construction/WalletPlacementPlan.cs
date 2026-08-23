using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Construction;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 財布を通る設置。素材を払うか残りで賄うかは計画時に確定済み
    /// A placement that goes through the wallet; whether it pays materials or draws from the remainder was settled when planning
    /// </summary>
    internal class WalletPlacementPlan : IConstructionPlacementPlan
    {
        public IReadOnlyList<(ItemId itemId, int count)> ItemsToConsume { get; }

        private readonly IRemainingPlacementCountMutation _mutation;
        private readonly ConstructionPayerDataStore _payers;
        private readonly ConstructionWalletUsage _usage;
        private readonly int _playerId;
        private readonly BlockId _walletBlockId;
        private readonly int _placementsPerCost;

        internal WalletPlacementPlan(IReadOnlyList<(ItemId itemId, int count)> itemsToConsume, IRemainingPlacementCountMutation mutation, ConstructionPayerDataStore payers,
            ConstructionWalletUsage usage, int playerId, BlockId walletBlockId, int placementsPerCost)
        {
            ItemsToConsume = itemsToConsume;
            _mutation = mutation;
            _payers = payers;
            _usage = usage;
            _playerId = playerId;
            _walletBlockId = walletBlockId;
            _placementsPerCost = placementsPerCost;
        }

        public void Commit(IOpenableInventory inventory, BlockInstanceId blockInstanceId)
        {
            ConstructionCostService.ConsumeRequiredItems(ItemsToConsume, inventory);

            // 素材を払ったセルは1セット分を補充してから1消費する（残り=N-1）
            // A cell that paid materials refills one set's worth and then consumes one (remaining = N-1)
            switch (_usage)
            {
                case ConstructionWalletUsage.CoveredByWallet:
                    _mutation.ConsumeOne(_playerId, _walletBlockId);
                    break;
                case ConstructionWalletUsage.PaidAndRefilled:
                    _mutation.Refill(_playerId, _walletBlockId, _placementsPerCost);
                    _mutation.ConsumeOne(_playerId, _walletBlockId);
                    break;
                case ConstructionWalletUsage.NotUsed:
                default:
                    throw new ArgumentOutOfRangeException(nameof(_usage), _usage, null);
            }

            // 撤去時に同じ財布へ戻すため課金元を覚える
            // Remember who paid so the removal returns to the very same wallet
            _payers.SetPayer(blockInstanceId, _playerId);
        }
    }
}
