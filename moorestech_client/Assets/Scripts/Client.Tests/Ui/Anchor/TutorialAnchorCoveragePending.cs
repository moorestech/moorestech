using System.Collections.Generic;
using Client.Game.InGame.Tutorial.UIHighlight;

namespace Client.Tests.Ui.Anchor
{
    /// <summary>
    /// UITK で未移植の画面のアンカー（id → 印を付けるレーン）。規則: 印を付けたレーンが同じ PR で自分の行を消す。行を残したまま印を付けると TutorialAnchorCoverageTest が落ちる。S5-ladder で空になる（plan §4.5・execution-plan E-1）
    /// Anchors of screens not yet ported to UITK (id → lane that marks them). Rule: the marking lane deletes its rows in the same PR; marking while the row remains fails TutorialAnchorCoverageTest. S5-ladder empties the table (plan §4.5, execution-plan E-1)
    /// </summary>
    public static class TutorialAnchorCoveragePending
    {
        public static readonly IReadOnlyDictionary<string, string> Owners = new Dictionary<string, string>
        {
            { AnchorIds.HotbarHud, "S3-hotbar-modal" },
            { AnchorIds.ChallengePanel, "S5-challenge" },
            { AnchorIds.ChallengeCategories, "S5-challenge" },
            { AnchorIds.ChallengeNodePrefix, "S5-challenge" },
        };
    }
}
