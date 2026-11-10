using System;
using System.Collections.Generic;
using Client.Ui.State.Research.Logic;
using Core.Master;
using UnityEngine;

namespace Client.Ui.State.Research.Catalog
{
    /// <summary>
    /// 研究ノード 1 件の静的な定義。マスタから 1 回だけ写し、サーバーの状態とは混ぜない
    /// Static definition of one research node; copied once from the master and never mixed with server state
    /// </summary>
    public sealed class ResearchNodeDefinition
    {
        public Guid Guid { get; }
        public ItemId IconItemId { get; }
        // ツリー座標（Y 上向き。NodeGraphNode と同じ向き）
        // Tree coordinates (Y up, same as NodeGraphNode)
        public Vector2 TreePosition { get; }
        public IReadOnlyList<Guid> PreviousGuids { get; }
        public IReadOnlyList<ResearchConsumeItem> ConsumeItems { get; }
        public ResearchUnlockSet Unlocks { get; }

        public ResearchNodeDefinition(Guid guid, ItemId iconItemId, Vector2 treePosition, IReadOnlyList<Guid> previousGuids, IReadOnlyList<ResearchConsumeItem> consumeItems, ResearchUnlockSet unlocks)
        {
            Guid = guid;
            IconItemId = iconItemId;
            TreePosition = treePosition;
            PreviousGuids = previousGuids;
            ConsumeItems = consumeItems;
            Unlocks = unlocks;
        }
    }
}
