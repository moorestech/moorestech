using System;
using System.Collections.Generic;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧の判断（注目点・状態の文言）。web: moorestech_web/webui/src/features/challenge/challengeLogic.ts・ChallengeNodeCard.tsx
    /// Challenge list decisions (focus, state label); web: moorestech_web/webui/src/features/challenge/challengeLogic.ts and ChallengeNodeCard.tsx
    /// </summary>
    public static class ChallengeListLogic
    {
        // 寄せ先は並びで最初の進行中。無ければ寄せない
        // Focus the first in-progress node in order; none means no focus
        public static bool TryFindInitialFocusNode(IReadOnlyList<ChallengeNodeCardViewModel> nodes, out ChallengeNodeCardViewModel focusNode)
        {
            foreach (var node in nodes)
            {
                if (node.State != ChallengeNodeState.Current) continue;
                focusNode = node;
                return true;
            }
            focusNode = null;
            return false;
        }

        // 状態の文言キー（前例 ResearchLogic.DeriveNodeStateLabelKey）
        // State label key (precedent ResearchLogic.DeriveNodeStateLabelKey)
        public static LocalizationKey DeriveStateLabelKey(ChallengeNodeState state)
        {
            return state switch
            {
                ChallengeNodeState.Locked => LocalizationKeys.Ui.Challenge.StateLocked,
                ChallengeNodeState.Current => LocalizationKeys.Ui.Challenge.StateCurrent,
                ChallengeNodeState.Completed => LocalizationKeys.Ui.Challenge.StateCompleted,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
            };
        }
    }
}
