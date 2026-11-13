using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.State.Challenge.Tree
{
    /// <summary>
    /// チャレンジのカード 1 枚の表示
    /// Display of one challenge card
    /// </summary>
    public sealed class ChallengeNodeCardViewModel
    {
        public Guid Guid { get; }
        public Vector2 TreePosition { get; }
        public Vector2 Scale { get; }
        public IReadOnlyList<Guid> PreviousGuids { get; }
        public ChallengeNodeState State { get; }
        public LocalizationKey StateLabelKey { get; }

        public ChallengeNodeCardViewModel(Guid guid, Vector2 treePosition, Vector2 scale, IReadOnlyList<Guid> previousGuids, ChallengeNodeState state)
        {
            Guid = guid;
            TreePosition = treePosition;
            Scale = scale;
            PreviousGuids = previousGuids;
            State = state;

            // 文言キーは状態だけで決まるのでここで引く（状態と文言が食い違う VM を作れない）
            // The label key follows from the state alone, so derive it here (no VM can carry a mismatched label)
            StateLabelKey = ChallengeListLogic.DeriveStateLabelKey(state);
        }

        // グラフへ渡す置き方が同じか
        // Whether the placement handed to the graph matches
        public bool HasSameGraphPlacementAs(ChallengeNodeCardViewModel other)
        {
            return Guid == other.Guid && TreePosition == other.TreePosition && PreviousGuids.SequenceEqual(other.PreviousGuids);
        }
    }
}
