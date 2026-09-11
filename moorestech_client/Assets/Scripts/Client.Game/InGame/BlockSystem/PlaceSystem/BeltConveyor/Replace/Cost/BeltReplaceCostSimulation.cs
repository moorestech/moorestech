using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Core.Item.Interface;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace.Cost
{
    /// <summary>
    /// セル逐次シミュレーションの結果。不足表示に使う素材と、払えなかったセル・見積り不確実なセルを持つ
    /// The result of the cell-by-cell simulation; it holds the materials the shortage display sees, the cells that could not be paid for, and the cells whose estimate is uncertain
    /// </summary>
    public class BeltReplaceCostSimulation
    {
        // 不足表示はPlaceableを落とす前に読むので、可否の適用と分けて持つ
        // The shortage display reads this before Placeable is cleared, so the verdict is applied separately
        public readonly IReadOnlyList<IItemStack> CostCheckItems;

        private readonly List<PlaceInfo> _unaffordableCells;
        private readonly List<int> _uncertainCellIndices;

        internal BeltReplaceCostSimulation(IReadOnlyList<IItemStack> costCheckItems, List<PlaceInfo> unaffordableCells, List<int> uncertainCellIndices)
        {
            CostCheckItems = costCheckItems;
            _unaffordableCells = unaffordableCells;
            _uncertainCellIndices = uncertainCellIndices;
        }

        public void MarkUnaffordableCellsAsNotPlaceable()
        {
            if (_unaffordableCells == null) return;
            foreach (var placeInfo in _unaffordableCells) placeInfo.Placeable = false;
        }

        // 見積り不確実なセルは送信したうえで色だけ分ける。添字は直前のSetPreviewの並び順と一致する
        // Uncertain cells are still sent and only differ in color; the indices match the ordering of the preceding SetPreview
        public void ApplyUncertainRefundColors(IPlacementPreviewBlockGameObjectController previewController)
        {
            if (_uncertainCellIndices == null) return;
            foreach (var index in _uncertainCellIndices)
            {
                if (previewController.TryGetPreviewBlock(index, out var previewBlock)) previewBlock.SetUncertainReplaceColor();
            }
        }
    }
}
