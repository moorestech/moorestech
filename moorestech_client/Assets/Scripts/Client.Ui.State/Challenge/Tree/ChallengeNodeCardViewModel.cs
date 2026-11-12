using System;
using System.Collections.Generic;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Ui.State.Challenge.Tree
{
    /// <summary>
    /// チャレンジのカード1枚の表示（座標は Y 上向きのツリー座標・倍率はマスタの UIScale の x と y）
    /// Display of one challenge card (Y-up tree position; scale is the master UIScale x and y)
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

        // グラフへ渡す置き方（キー・座標・線）が同じか。状態と倍率はカードの描き直しで足りる
        // Whether the graph placement (key, position, edges) matches; state and scale only need a card redraw
        public bool HasSameGraphPlacementAs(ChallengeNodeCardViewModel other)
        {
            return Guid == other.Guid && TreePosition == other.TreePosition && ChallengeListLogic.SameGuidSequence(PreviousGuids, other.PreviousGuids);
        }
    }
}
