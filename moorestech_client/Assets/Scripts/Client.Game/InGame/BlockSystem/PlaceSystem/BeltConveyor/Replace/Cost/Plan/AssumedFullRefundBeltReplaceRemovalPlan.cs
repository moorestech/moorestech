using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace.Cost.Plan
{
    /// <summary>
    /// 課金元が自分でない場合を見込んだ撤去。サーバーは設置して支払った人の財布で返却を決めるため、全額戻る側に倒して「送れる」ことだけを保証する
    /// A removal that assumes the payer is someone else; the server decides the refund from whoever placed and paid, so this leans to the full-refund side purely to keep the cell sendable
    ///
    /// 返却が届いた扱いにはしない。不足表示は自分の財布で見た確実な返却だけを見る
    /// The refund is never treated as arrived; the shortage display still sees only the certain refunds from the player's own wallet
    /// </summary>
    internal class AssumedFullRefundBeltReplaceRemovalPlan : IBeltReplaceRemovalPlan
    {
        public IReadOnlyList<IItemStack> RefundItems { get; }

        private readonly BlockId _walletBlockId;

        internal AssumedFullRefundBeltReplaceRemovalPlan(IReadOnlyList<IItemStack> refundItems, BlockId walletBlockId)
        {
            RefundItems = refundItems;
            _walletBlockId = walletBlockId;
        }

        public void Commit(BeltReplaceCostLedger ledger)
        {
            ledger.AddAssumedRefund(RefundItems);

            // 全額戻る＝凝縮した側を仮定するので、残りは空へ戻る
            // Assuming the full refund means assuming the condensed side, so the remainder resets to empty
            var remaining = ledger.GetWalletRemainder(_walletBlockId);
            ledger.SetWalletRemainder(_walletBlockId, ConstructionWalletUtil.AdvanceOnRemoval(remaining, true));
        }
    }
}
