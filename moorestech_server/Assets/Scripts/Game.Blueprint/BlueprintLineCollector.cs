using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;

namespace Game.Blueprint
{
    /// <summary>
    ///     コピー対象内で両端が閉じた電線・歯車チェーンをindex化する
    ///     Indexes wires and gear chains whose both ends lie inside the copy targets
    /// </summary>
    public static class BlueprintLineCollector
    {
        public static (List<BlueprintLineJsonObject> wires, List<BlueprintLineJsonObject> chains) Collect(IReadOnlyList<IBlock> copyTargets)
        {
            var indexById = new Dictionary<BlockInstanceId, int>();
            for (var i = 0; i < copyTargets.Count; i++) indexById[copyTargets[i].BlockInstanceId] = i;

            var wires = new List<BlueprintLineJsonObject>();
            var chains = new List<BlueprintLineJsonObject>();
            for (var i = 0; i < copyTargets.Count; i++)
            {
                CollectWires(i);
                CollectChains(i);
            }

            return (wires, chains);

            #region Internal

            // 同じ線を両端から二重に拾わないよう、自分より後ろのindexの相手だけ採る
            // Take only partners with a larger index so each line is collected once
            void CollectWires(int selfIndex)
            {
                if (!copyTargets[selfIndex].ComponentManager.TryGetComponent<IElectricWireConnector>(out var connector)) return;
                foreach (var (partnerId, connection) in connector.WireConnections)
                {
                    if (!indexById.TryGetValue(partnerId, out var partnerIndex) || partnerIndex <= selfIndex) continue;
                    wires.Add(new BlueprintLineJsonObject(selfIndex, partnerIndex, connection.Record.ConnectToolGuid));
                }
            }

            // チェーンは接続一覧を公開しないため、BP内の後方ブロックとの記録を引く
            // Chains expose no connection list, so look up the record against later blocks in the blueprint
            void CollectChains(int selfIndex)
            {
                if (!copyTargets[selfIndex].ComponentManager.TryGetComponent<IGearChainPole>(out var pole)) return;
                for (var partnerIndex = selfIndex + 1; partnerIndex < copyTargets.Count; partnerIndex++)
                {
                    if (!pole.TryGetChainConnectionRecord(copyTargets[partnerIndex].BlockInstanceId, out var record)) continue;
                    chains.Add(new BlueprintLineJsonObject(selfIndex, partnerIndex, record.ConnectToolGuid));
                }
            }

            #endregion
        }
    }
}
