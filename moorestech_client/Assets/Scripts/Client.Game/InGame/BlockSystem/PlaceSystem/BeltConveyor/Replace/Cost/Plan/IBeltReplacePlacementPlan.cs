using System.Collections.Generic;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace.Cost.Plan
{
    /// <summary>
    /// 張替え1セルの設置指示。サーバーのIConstructionPlacementPlanと同型で、見えるのは消費素材列と確定操作のみ
    /// One replace cell's placement instruction, shaped like the server's IConstructionPlacementPlan; only the materials to consume and the commit are visible
    /// </summary>
    internal interface IBeltReplacePlacementPlan
    {
        IReadOnlyList<(ItemId itemId, int count)> ItemsToConsume { get; }

        // 設置を通すと決めた後にのみ呼ぶ。呼ばなければ台帳は変わらない
        // Call only after the cell is decided to go through; skipping it leaves the ledger untouched
        void Commit(BeltReplaceCostLedger ledger);
    }
}
