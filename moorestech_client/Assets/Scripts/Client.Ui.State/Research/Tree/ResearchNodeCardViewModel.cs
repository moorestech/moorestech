using System;
using System.Collections.Generic;
using Client.Ui.State.Research.Logic;
using Core.Master;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.State.Research.Tree
{
    /// <summary>
    /// 研究ノードのカード 1 枚の表示（4 状態・状態ラベル・選択）
    /// Display of one research node card (four states, state label, selection)
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
    }
}
