using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.Research.Logic;
using Core.Master;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.State.Research.Tree
{
    /// <summary>
    /// 研究ノードのカード1枚の表示
    /// Display of one research node card
    /// </summary>
    public sealed class ResearchNodeCardViewModel
    {
        public Guid Guid { get; }
        public ItemId IconItemId { get; }
        public Vector2 TreePosition { get; }
        public IReadOnlyList<Guid> PreviousGuids { get; }
        public NodeCardState CardState { get; }
        public LocalizationKey StateLabelKey { get; }
        public bool Selected { get; }

        public ResearchNodeCardViewModel(Guid guid, ItemId iconItemId, Vector2 treePosition, IReadOnlyList<Guid> previousGuids, NodeCardState cardState, LocalizationKey stateLabelKey, bool selected)
        {
            Guid = guid;
            IconItemId = iconItemId;
            TreePosition = treePosition;
            PreviousGuids = previousGuids;
            CardState = cardState;
            StateLabelKey = stateLabelKey;
            Selected = selected;
        }

        // グラフへ渡す置き方が同じか（チャレンジの HasSameGraphPlacementAs と同じ基準）
        // Whether the placement handed to the graph matches (same criteria as the challenge HasSameGraphPlacementAs)
        public bool HasSameGraphPlacementAs(ResearchNodeCardViewModel other)
        {
            return Guid == other.Guid && TreePosition == other.TreePosition && PreviousGuids.SequenceEqual(other.PreviousGuids);
        }
    }
}
