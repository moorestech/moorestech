using System;
using System.Collections.Generic;
using Core.Master;
using Game.Research;

namespace Client.Ui.State.Research.Logic
{
    // 研究で消費するアイテム 1 件。AcceptedItemIds は上位レベルを含む受け付け一覧
    // One item a research consumes; AcceptedItemIds includes higher-level variants
    public sealed class ResearchConsumeItem
    {
        public ItemId ItemId { get; }
        public int Count { get; }
        public IReadOnlyList<ItemId> AcceptedItemIds { get; }

        public ResearchConsumeItem(ItemId itemId, int count, IReadOnlyList<ItemId> acceptedItemIds)
        {
            ItemId = itemId;
            Count = count;
            AcceptedItemIds = acceptedItemIds;
        }
    }

    // 初期フォーカスの判断が読むノードの要約
    // The node summary the initial-focus decision reads
    public readonly struct ResearchNodeSummary
    {
        public Guid Guid { get; }
        public ResearchNodeState State { get; }

        public ResearchNodeSummary(Guid guid, ResearchNodeState state)
        {
            Guid = guid;
            State = state;
        }
    }
}
