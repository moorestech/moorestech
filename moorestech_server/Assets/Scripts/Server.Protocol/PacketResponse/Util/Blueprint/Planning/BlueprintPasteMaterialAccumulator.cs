using System.Collections.Generic;
using Core.Master;
using Game.Construction;

namespace Server.Protocol.PacketResponse.Util.Blueprint.Planning
{
    /// <summary>
    /// 受理候補のセル数と配線素材だけを保持する累積費用
    /// Cumulative cost holding only accepted candidate cell counts and line materials
    /// </summary>
    internal sealed class BlueprintPasteMaterialAccumulator
    {
        private readonly ConstructionWalletQuery _wallet;
        private readonly bool _isPaymentWaived;
        private readonly Dictionary<BlockId, int> _cellCounts = new();
        private readonly Dictionary<ItemId, int> _lineCounts = new();

        internal BlueprintPasteMaterialAccumulator(ConstructionWalletQuery wallet, bool isPaymentWaived)
        {
            _wallet = wallet;
            _isPaymentWaived = isPaymentWaived;
        }

        // コピーを一度だけ走査し、後続の費用照会で再走査しない
        // Scan each copy once and avoid revisiting it for later cost queries
        internal void AddDraft(BlueprintPasteCopyDraft draft)
        {
            for (var i = 0; i < draft.Elements.Count; i++)
            {
                if (!draft.NonOverlapFlags[i]) continue;
                var blockId = draft.Elements[i].BlockId;
                _cellCounts.TryGetValue(blockId, out var count);
                _cellCounts[blockId] = count + 1;
            }

            // 接続不能線は描画には残るが実行も支払いも行わない
            // Unconnectable lines remain for previews but are neither placed nor paid for
            foreach (var line in draft.Lines)
            {
                if (!line.IsConnectable) continue;
                if (_isPaymentWaived && line.Kind == BlueprintPasteLineKind.ElectricWire) continue;
                foreach (var material in line.Materials)
                {
                    _lineCounts.TryGetValue(material.ItemId, out var count);
                    _lineCounts[material.ItemId] = count + material.Count;
                }
            }
        }

        // 財布が確定したブロック素材と線素材を同一ItemIdへ合算する
        // Combine wallet-decided block costs and line costs by ItemId
        internal List<(ItemId itemId, int count)> GetRequiredItems()
        {
            var totals = new Dictionary<ItemId, int>();
            if (!_isPaymentWaived)
            {
                foreach (var (itemId, count) in _wallet.GetItemsToConsumeForCells(_cellCounts))
                {
                    totals[itemId] = count;
                }
            }

            foreach (var (itemId, count) in _lineCounts)
            {
                totals.TryGetValue(itemId, out var previous);
                totals[itemId] = previous + count;
            }

            var required = new List<(ItemId itemId, int count)>(totals.Count);
            foreach (var (itemId, count) in totals)
            {
                if (count != 0) required.Add((itemId, count));
            }
            return required;
        }
    }
}
