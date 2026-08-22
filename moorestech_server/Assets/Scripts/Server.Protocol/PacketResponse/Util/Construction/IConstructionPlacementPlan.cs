using System.Collections.Generic;
using Core.Master;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 設置時に財布が返す指示。呼び出し側に見えるのは消費すべき素材列だけ
    /// The placement instruction from the wallet; all a caller can see is which materials to consume
    /// </summary>
    public interface IConstructionPlacementPlan
    {
        // 消費すべき素材。空なら何も消費しない
        // Materials to consume; empty means consume nothing
        IReadOnlyList<(ItemId itemId, int count)> ItemsToConsume { get; }
    }
}
