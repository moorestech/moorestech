using System.Collections.Generic;
using Core.Item.Interface;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace.Cost.Plan
{
    /// <summary>
    /// 張替え1セルの撤去指示。サーバーのIConstructionRemovalPlanと同型で、見えるのは返却アイテム列と確定操作のみ
    /// One replace cell's removal instruction, shaped like the server's IConstructionRemovalPlan; only the items handed back and the commit are visible
    /// </summary>
    internal interface IBeltReplaceRemovalPlan
    {
        // 自分の財布で見積もった返却。台帳を進めるときの最良推定として使う
        // The refund estimated against the player's own wallet, used as the best guess when advancing the ledger
        IReadOnlyList<IItemStack> RefundItems { get; }

        // 課金元が誰であっても確実に届く返却。支払いが確実かはこちらで判定する
        // The refund that arrives whoever the payer is; whether a payment is certain is judged against this
        IReadOnlyList<IItemStack> GuaranteedRefundItems { get; }

        void Commit(BeltReplaceCostLedger ledger);
    }
}
