using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace
{
    /// <summary>
    /// 張替え1セルの撤去計画。財布を通らないブロックはWalletBlockIdがnull
    /// One replace cell's removal plan; WalletBlockId is null for a block that bypasses the wallet
    /// </summary>
    internal readonly struct BeltReplaceRemovalPlan
    {
        public readonly IReadOnlyList<IItemStack> RefundItems;
        public readonly BlockId? WalletBlockId;
        public readonly bool Condensed;

        internal BeltReplaceRemovalPlan(IReadOnlyList<IItemStack> refundItems, BlockId? walletBlockId, bool condensed)
        {
            RefundItems = refundItems;
            WalletBlockId = walletBlockId;
            Condensed = condensed;
        }
    }
}
