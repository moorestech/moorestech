using System;
using System.Collections.Generic;
using Core.Item;
using Core.Master;
using Game.Research;

namespace Client.Network.API
{
    // 同期済みの研究完了状態からスタックレベルを導出する（専用同期プロトコルは持たない）
    // Derive stack levels from already-synced research completion state (no dedicated sync protocol)
    public static class ResearchItemStackLevelApplier
    {
        public static void ApplyCompleted(Dictionary<Guid, ResearchNodeState> nodeStates)
        {
            foreach (var (researchGuid, state) in nodeStates)
            {
                if (state != ResearchNodeState.Completed) continue;
                Apply(researchGuid);
            }
        }

        public static void Apply(Guid researchGuid)
        {
            var research = MasterHolder.ResearchMaster.GetResearch(researchGuid);
            ItemStackLevelDataStore.Instance.ApplyUnlockItemStackLevelActions(research.ClearedActions.items);
        }
    }
}
