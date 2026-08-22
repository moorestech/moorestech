using System.Collections.Generic;
using Core.Master;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 財布が返す設置指示。見えるのは消費素材列のみ
    /// The placement instruction from the wallet; only the materials to consume are visible
    /// </summary>
    public interface IConstructionPlacementPlan
    {
        // 消費すべき素材。空なら何も消費しない
        // Materials to consume; empty means consume nothing
        IReadOnlyList<(ItemId itemId, int count)> ItemsToConsume { get; }
    }
}
