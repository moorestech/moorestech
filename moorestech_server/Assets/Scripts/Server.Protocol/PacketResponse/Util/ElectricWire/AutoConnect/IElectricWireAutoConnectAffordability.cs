using System.Collections.Generic;
using Core.Master;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// 自動接続の電線素材を賄えるかの判定。在庫の持ち方（予約込み実在庫・無料設置）は実装側に閉じ、選定ループは条件を知らない（ADR 0056）
    /// Whether auto-connect wire materials are affordable; how stock is held (reserved real inventory or free placement) stays in the implementation so the selection loop never branches on it (ADR 0056)
    /// </summary>
    public interface IElectricWireAutoConnectAffordability
    {
        bool CanAfford(IReadOnlyDictionary<ItemId, int> requiredByItem);
    }
}
