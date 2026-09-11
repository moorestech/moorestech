using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace.Cost
{
    /// <summary>
    /// 張替え列を1セルずつ進める間の可変状態。所持数・財布残り・実際に届いた返却を持ち、Planはここへ確定を書き込む
    /// The mutable state carried while a replace run advances cell by cell; it holds the item counts, the wallet remainders and the refunds that actually arrived, and the plans commit into it
    /// </summary>
    internal class BeltReplaceCostLedger
    {
        // 不足表示が見るのは「実際に届いた返却」だけ。仮定した返却は混ぜない
        // The shortage display sees only the refunds that actually arrived; assumed refunds never mix in
        public IReadOnlyList<IItemStack> ArrivedRefunds => _arrivedRefunds;

        private readonly ConstructionWalletQuery _walletQuery;

        // 財布の残りは財布キー（坂は直線代表）で数え、撤去側と設置側の両方がここを進める
        // The remainders are keyed by the wallet block (slopes normalize to the straight one) and both the removal and the placement advance them
        private readonly Dictionary<BlockId, int> _walletRemainders = new();
        private readonly Dictionary<ItemId, int> _availableItemCounts = new();
        private readonly List<IItemStack> _arrivedRefunds = new();

        internal BeltReplaceCostLedger(ConstructionWalletQuery walletQuery, IEnumerable<IItemStack> inventoryItems)
        {
            _walletQuery = walletQuery;
            foreach (var stack in inventoryItems) AddAvailableItem(stack.Id, stack.Count);
        }

        // 消費素材を賄えるか。判定規則はサーバーと同じConstructionCostRulesが持つ
        // Whether the materials can be covered; ConstructionCostRules, the very one the server uses, owns the rule
        public bool CanPay(IReadOnlyList<(ItemId itemId, int count)> itemsToConsume, IReadOnlyList<IItemStack> incomingItems)
        {
            return ConstructionCostRules.HasRequiredItems(itemsToConsume, _availableItemCounts, incomingItems);
        }

        public void ConsumeItems(IReadOnlyList<(ItemId itemId, int count)> itemsToConsume)
        {
            foreach (var (itemId, count) in itemsToConsume) AddAvailableItem(itemId, -count);
        }

        public void AddArrivedRefund(IReadOnlyList<IItemStack> refundItems)
        {
            foreach (var refundItem in refundItems)
            {
                AddAvailableItem(refundItem.Id, refundItem.Count);
                _arrivedRefunds.Add(refundItem);
            }
        }

        // 届くと仮定した返却。後続セルの支払い原資にはなるが、不足表示には現れない
        // A refund assumed to arrive; it funds the later cells but never shows up in the shortage display
        public void AddAssumedRefund(IReadOnlyList<IItemStack> refundItems)
        {
            foreach (var refundItem in refundItems) AddAvailableItem(refundItem.Id, refundItem.Count);
        }

        // 初出の財布はクライアントのミラーが持つ現在値から始める
        // A wallet seen for the first time starts from the current value held by the client mirror
        public int GetWalletRemainder(BlockId walletBlockId)
        {
            if (_walletRemainders.TryGetValue(walletBlockId, out var remaining)) return remaining;
            return _walletQuery.GetWalletStatus(walletBlockId)?.RemainingCount ?? 0;
        }

        public void SetWalletRemainder(BlockId walletBlockId, int remaining)
        {
            _walletRemainders[walletBlockId] = remaining;
        }

        private void AddAvailableItem(ItemId itemId, int count)
        {
            _availableItemCounts.TryGetValue(itemId, out var current);
            _availableItemCounts[itemId] = current + count;
        }
    }
}
