using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace.Cost.Plan
{
    /// <summary>
    /// 財布を通る撤去を、操作プレイヤー自身の財布で見積もったもの。凝縮するかは計画時に確定済み
    /// A removal through the wallet, estimated against the operating player's own wallet; whether it condenses was settled when planning
    /// </summary>
    internal class WalletBeltReplaceRemovalPlan : IBeltReplaceRemovalPlan
    {
        public IReadOnlyList<IItemStack> RefundItems { get; }

        private readonly BlockId _walletBlockId;
        private readonly bool _condensed;

        internal WalletBeltReplaceRemovalPlan(IReadOnlyList<IItemStack> refundItems, BlockId walletBlockId, bool condensed)
        {
            RefundItems = refundItems;
            _walletBlockId = walletBlockId;
            _condensed = condensed;
        }

        public void Commit(BeltReplaceCostLedger ledger)
        {
            ledger.AddArrivedRefund(RefundItems);

            // 残高の進め方はサーバーと同じConstructionWalletUtilが所有する
            // ConstructionWalletUtil, the very one the server uses, owns how the remainder advances
            var remaining = ledger.GetWalletRemainder(_walletBlockId);
            ledger.SetWalletRemainder(_walletBlockId, ConstructionWalletUtil.AdvanceOnRemoval(remaining, _condensed));
        }
    }
}
