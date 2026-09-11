using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace.Cost.Plan
{
    /// <summary>
    /// 財布を通る撤去を、操作プレイヤー自身の財布で見積もったもの。凝縮するかは計画時に確定済み
    /// A removal through the wallet, estimated against the operating player's own wallet; whether it condenses was settled when planning
    ///
    /// 実際の課金元は別人かもしれないので、この見積りは確実な返却ではない
    /// The real payer may be someone else, so this estimate is never a guaranteed refund
    /// </summary>
    internal class WalletBeltReplaceRemovalPlan : IBeltReplaceRemovalPlan
    {
        private static readonly IReadOnlyList<IItemStack> NoGuaranteedRefund = Array.Empty<IItemStack>();

        public IReadOnlyList<IItemStack> RefundItems { get; }

        // 凝縮するかは課金元の財布が決めるため、自分の財布の見積りはどちら向きにも外れうる
        // Whether it condenses is decided by the payer's wallet, so the player's own estimate can miss in either direction
        public IReadOnlyList<IItemStack> GuaranteedRefundItems => NoGuaranteedRefund;

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
