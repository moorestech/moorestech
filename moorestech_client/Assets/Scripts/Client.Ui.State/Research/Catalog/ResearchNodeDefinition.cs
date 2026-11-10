using System;
using System.Collections.Generic;
using Client.Ui.State.Research.Logic;
using Core.Master;
using UnityEngine;

namespace Client.Ui.State.Research.Catalog
{
    /// <summary>
    /// 研究ノード1件の静的定義
    /// Static definition of one research node
    /// </summary>
    public sealed class ResearchNodeDefinition
    {
        public Guid Guid { get; }
        public ItemId IconItemId { get; }
        // ツリー座標（Y上向き）
        // Tree coordinates (Y up)
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
