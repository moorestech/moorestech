using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 財布が返す設置指示。見えるのは消費素材列と確定操作のみ
    /// The placement instruction from the wallet; only the materials to consume and the commit are visible
    /// </summary>
    public interface IConstructionPlacementPlan
    {
        // 消費すべき素材。空なら何も消費しない
        // Materials to consume; empty means consume nothing
        IReadOnlyList<(ItemId itemId, int count)> ItemsToConsume { get; }

        // 設置確定後にのみ呼ぶ。呼ばなければ財布も素材も変わらない
        // Call only after placement is confirmed; skipping it leaves both wallet and materials untouched
        void Commit(IOpenableInventory inventory, BlockInstanceId blockInstanceId);
    }
}
