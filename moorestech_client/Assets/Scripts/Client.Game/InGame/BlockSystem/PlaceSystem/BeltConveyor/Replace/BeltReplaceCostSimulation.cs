using System.Collections.Generic;
using Core.Item.Interface;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace
{
    /// <summary>
    /// セル逐次シミュレーションの結果。不足表示に使う素材と、払えなかったセルを持つ
    /// The result of the cell-by-cell simulation; it holds the materials the shortage display sees and the cells that could not be paid for
    /// </summary>
    public class BeltReplaceCostSimulation
    {
        // 不足表示はPlaceableを落とす前に読むので、可否の適用と分けて持つ
        // The shortage display reads this before Placeable is cleared, so the verdict is applied separately
        public readonly IReadOnlyList<IItemStack> CostCheckItems;

        private readonly List<PlaceInfo> _unaffordableCells;

        public BeltReplaceCostSimulation(IReadOnlyList<IItemStack> costCheckItems, List<PlaceInfo> unaffordableCells)
        {
            CostCheckItems = costCheckItems;
            _unaffordableCells = unaffordableCells;
        }

        public void MarkUnaffordableCellsAsNotPlaceable()
        {
            if (_unaffordableCells == null) return;
            foreach (var placeInfo in _unaffordableCells) placeInfo.Placeable = false;
        }
    }
}
