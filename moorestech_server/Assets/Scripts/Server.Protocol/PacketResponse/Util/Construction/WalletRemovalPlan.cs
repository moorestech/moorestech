using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Construction;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 財布を通る撤去。凝縮するかと戻し先の財布は計画時に確定済み
    /// A removal that goes through the wallet; whether it condenses and whose wallet receives it were settled when planning
    /// </summary>
    internal class WalletRemovalPlan : IConstructionRemovalPlan
    {
        public IReadOnlyList<IItemStack> ItemsToRefund { get; }

        private readonly IRemainingPlacementCountMutation _mutation;
        private readonly ConstructionPayerDataStore _payers;
        private readonly int _payerPlayerId;
        private readonly BlockId _walletBlockId;
        private readonly BlockInstanceId _blockInstanceId;
        private readonly bool _condensed;

        internal WalletRemovalPlan(IReadOnlyList<IItemStack> itemsToRefund, IRemainingPlacementCountMutation mutation, ConstructionPayerDataStore payers,
            int payerPlayerId, BlockId walletBlockId, BlockInstanceId blockInstanceId, bool condensed)
        {
            ItemsToRefund = itemsToRefund;
            _mutation = mutation;
            _payers = payers;
            _payerPlayerId = payerPlayerId;
            _walletBlockId = walletBlockId;
            _blockInstanceId = blockInstanceId;
            _condensed = condensed;
        }

        public void Commit()
        {
            _mutation.ApplyReturn(_payerPlayerId, _walletBlockId, _condensed);
            _payers.RemovePayer(_blockInstanceId);
        }
    }
}
