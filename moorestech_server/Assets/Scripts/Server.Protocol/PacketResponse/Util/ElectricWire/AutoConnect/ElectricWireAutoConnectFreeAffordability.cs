using System.Collections.Generic;
using Core.Master;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// 無料設置デバッグの所持判定。素材を消費しないため常に賄えるとみなす（ADR 0056）
    /// Affordability under the free-placement debug; nothing is consumed, so it always counts as affordable (ADR 0056)
    /// </summary>
    public class ElectricWireAutoConnectFreeAffordability : IElectricWireAutoConnectAffordability
    {
        public bool CanAfford(IReadOnlyDictionary<ItemId, int> requiredByItem)
        {
            return true;
        }
    }
}
